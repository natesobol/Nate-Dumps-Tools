using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MsgReader.Outlook;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 50; // 50 MB
});

builder.Services.AddResponseCompression();

var app = builder.Build();

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/extract", async Task<IResult> (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with email uploads." });
    }

    var form = await request.ReadFormAsync();
    var includeAttachments = TryParseBool(form["includeAttachments"], false);
    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest(new { error = "Upload at least one .eml, .msg, or .mbox file." });
    }

    var responses = new List<ExtractedFile>();

    foreach (var file in files)
    {
        try
        {
            responses.Add(await ProcessFileAsync(file, includeAttachments));
        }
        catch (Exception ex)
        {
            responses.Add(new ExtractedFile
            {
                FileName = file.FileName,
                Error = $"Failed to process file: {ex.Message}"
            });
        }
    }

    var flattenedMessages = responses
        .Where(r => r.Messages is not null)
        .SelectMany(r => r.Messages!.Select(m => (File: r.FileName, Message: m)))
        .OrderBy(m => SafeTimestamp(m.Message.Timestamp))
        .ToList();

    var threads = BuildThreads(flattenedMessages);
    var csv = BuildCsv(flattenedMessages);
    var bodyText = BuildBodyExport(flattenedMessages);
    var threadCsv = BuildThreadCsv(threads);
    var threadText = BuildThreadText(threads);

    return Results.Ok(new
    {
        files = responses,
        messageCount = flattenedMessages.Count,
        csv,
        bodyText,
        threads,
        threadCsv,
        threadText
    });
});

app.Run();

static async Task<ExtractedFile> ProcessFileAsync(IFormFile file, bool includeAttachments)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (extension is ".eml" or ".mbox" or ".txt")
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        var messages = new List<EmailMessage>();

        if (extension == ".mbox")
        {
            using var parser = new MimeParser(stream, MimeFormat.Mbox);
            while (!parser.IsEndOfStream)
            {
                var message = parser.ParseMessage();
                messages.Add(FromMimeMessage(message, includeAttachments));
            }
        }
        else
        {
            var message = MimeMessage.Load(stream);
            messages.Add(FromMimeMessage(message, includeAttachments));
        }

        return new ExtractedFile { FileName = file.FileName, Messages = messages };
    }

    if (extension == ".msg")
    {
        var tempPath = Path.GetTempFileName();
        await using var fs = File.OpenWrite(tempPath);
        await file.CopyToAsync(fs);
        fs.Close();

        using var msg = new Storage.Message(tempPath, FileAccess.Read);
        var message = FromMsgReader(msg, includeAttachments);
        File.Delete(tempPath);

        return new ExtractedFile { FileName = file.FileName, Messages = new List<EmailMessage> { message } };
    }

    return new ExtractedFile
    {
        FileName = file.FileName,
        Error = "Unsupported file type. Upload .eml, .msg, or .mbox files."
    };
}

static EmailMessage FromMimeMessage(MimeMessage message, bool includeAttachments)
{
    var body = ExtractBodyText(message.TextBody, message.HtmlBody);
    var attachments = new List<AttachmentInfo>();

    foreach (var part in message.Attachments.OfType<MimePart>())
    {
        var fileName = part.FileName ?? "attachment";
        using var memory = new MemoryStream();
        part.Content?.DecodeTo(memory);
        var bytes = memory.ToArray();

        attachments.Add(new AttachmentInfo
        {
            FileName = fileName,
            ContentType = part.ContentType?.MimeType ?? "application/octet-stream",
            Size = bytes.Length,
            Base64Data = includeAttachments ? Convert.ToBase64String(bytes) : null
        });
    }

    return new EmailMessage
    {
        Subject = message.Subject ?? string.Empty,
        Sender = message.Sender?.ToString() ?? message.From.Mailboxes.FirstOrDefault()?.ToString() ?? "",
        Recipients = new RecipientSet
        {
            To = message.To.Select(r => r.ToString()).ToList(),
            Cc = message.Cc.Select(r => r.ToString()).ToList(),
            Bcc = message.Bcc.Select(r => r.ToString()).ToList()
        },
        Timestamp = message.Date.UtcDateTime,
        BodyText = body,
        Attachments = attachments
    };
}

static EmailMessage FromMsgReader(Storage.Message message, bool includeAttachments)
{
    var attachments = new List<AttachmentInfo>();

    foreach (var attachment in message.Attachments)
    {
        using var memory = new MemoryStream();
        attachment.Save(memory);
        var bytes = memory.ToArray();

        attachments.Add(new AttachmentInfo
        {
            FileName = attachment.FileName ?? "attachment",
            ContentType = attachment.ContentType ?? "application/octet-stream",
            Size = bytes.Length,
            Base64Data = includeAttachments ? Convert.ToBase64String(bytes) : null
        });
    }

    var toRecipients = message.GetEmailRecipients(Storage.Recipient.RecipientType.To, false, false)
        .Select(r => r.Email)
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .ToList();

    var ccRecipients = message.GetEmailRecipients(Storage.Recipient.RecipientType.Cc, false, false)
        .Select(r => r.Email)
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .ToList();

    var bccRecipients = message.GetEmailRecipients(Storage.Recipient.RecipientType.Bcc, false, false)
        .Select(r => r.Email)
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .ToList();

    return new EmailMessage
    {
        Subject = message.Subject ?? string.Empty,
        Sender = message.Sender?.Email ?? string.Empty,
        Recipients = new RecipientSet
        {
            To = toRecipients,
            Cc = ccRecipients,
            Bcc = bccRecipients
        },
        Timestamp = message.SentOn?.UtcDateTime ?? DateTime.MinValue,
        BodyText = ExtractBodyText(message.BodyText, message.BodyRtf),
        Attachments = attachments
    };
}

static string ExtractBodyText(string? textBody, string? htmlBody)
{
    if (!string.IsNullOrWhiteSpace(textBody))
    {
        return textBody.Trim();
    }

    if (string.IsNullOrWhiteSpace(htmlBody))
    {
        return string.Empty;
    }

    var withoutTags = Regex.Replace(htmlBody, "<[^>]+>", " ");
    return WebUtility.HtmlDecode(withoutTags).Trim();
}

static bool TryParseBool(string? value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;

static string BuildCsv(IEnumerable<(string File, EmailMessage Message)> messages)
{
    var builder = new StringBuilder();
    builder.AppendLine("File,Index,Subject,Sender,To,Cc,Bcc,Timestamp,Attachments");

    var index = 1;
    foreach (var (file, message) in messages)
    {
        var attachments = string.Join("|", message.Attachments.Select(a => a.FileName));
        builder.AppendLine(string.Join(',', new[]
        {
            EscapeCsv(file),
            index.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(message.Subject),
            EscapeCsv(message.Sender),
            EscapeCsv(string.Join("; ", message.Recipients.To)),
            EscapeCsv(string.Join("; ", message.Recipients.Cc)),
            EscapeCsv(string.Join("; ", message.Recipients.Bcc)),
            EscapeCsv(message.Timestamp == DateTime.MinValue ? string.Empty : message.Timestamp.ToString("o")),
            EscapeCsv(attachments)
        }));
        index++;
    }

    return builder.ToString();
}

static List<EmailThread> BuildThreads(IEnumerable<(string File, EmailMessage Message)> messages)
{
    return messages
        .GroupBy(tuple => NormalizeSubject(tuple.Message.Subject))
        .Select(group =>
        {
            var ordered = group
                .OrderBy(m => SafeTimestamp(m.Message.Timestamp))
                .ToList();

            return new EmailThread
            {
                Subject = ordered.First().Message.Subject ?? string.Empty,
                NormalizedSubject = group.Key,
                Messages = ordered
                    .Select((entry, index) => new ThreadMessage
                    {
                        Order = index + 1,
                        FileName = entry.File,
                        Subject = entry.Message.Subject,
                        Sender = entry.Message.Sender,
                        Recipients = entry.Message.Recipients,
                        Timestamp = entry.Message.Timestamp,
                        BodyText = entry.Message.BodyText,
                        Attachments = entry.Message.Attachments
                    })
                    .ToList()
            };
        })
        .OrderByDescending(thread => thread.Messages.Count)
        .ToList();
}

static string BuildThreadCsv(IEnumerable<EmailThread> threads)
{
    var builder = new StringBuilder();
    builder.AppendLine("Thread Subject,Normalized Subject,Message #,File,Sender,To,Cc,Bcc,Timestamp,Attachments");

    foreach (var thread in threads)
    {
        foreach (var message in thread.Messages)
        {
            var attachments = string.Join("|", message.Attachments.Select(a => a.FileName));
            builder.AppendLine(string.Join(',', new[]
            {
                EscapeCsv(thread.Subject),
                EscapeCsv(thread.NormalizedSubject),
                message.Order.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(message.FileName),
                EscapeCsv(message.Sender),
                EscapeCsv(string.Join("; ", message.Recipients.To)),
                EscapeCsv(string.Join("; ", message.Recipients.Cc)),
                EscapeCsv(string.Join("; ", message.Recipients.Bcc)),
                EscapeCsv(message.Timestamp == DateTime.MinValue ? string.Empty : message.Timestamp.ToString("o")),
                EscapeCsv(attachments)
            }));
        }
    }

    return builder.ToString();
}

static string BuildThreadText(IEnumerable<EmailThread> threads)
{
    var builder = new StringBuilder();

    foreach (var thread in threads)
    {
        builder.AppendLine($"Subject: {thread.Subject}");
        builder.AppendLine($"Normalized Subject: {thread.NormalizedSubject}");
        builder.AppendLine($"Messages: {thread.Messages.Count}");
        builder.AppendLine();

        foreach (var message in thread.Messages)
        {
            builder.AppendLine($"Message #{message.Order} ({message.FileName})");
            builder.AppendLine($"From: {message.Sender}");
            builder.AppendLine($"To: {string.Join("; ", message.Recipients.To)}");
            if (message.Recipients.Cc.Count > 0)
            {
                builder.AppendLine($"Cc: {string.Join("; ", message.Recipients.Cc)}");
            }
            if (message.Recipients.Bcc.Count > 0)
            {
                builder.AppendLine($"Bcc: {string.Join("; ", message.Recipients.Bcc)}");
            }
            if (message.Timestamp != DateTime.MinValue)
            {
                builder.AppendLine($"Date: {message.Timestamp:O}");
            }
            builder.AppendLine();
            builder.AppendLine(message.BodyText);
            builder.AppendLine("----");
            builder.AppendLine();
        }

        builder.AppendLine(new string('=', 40));
        builder.AppendLine();
    }

    return builder.ToString();
}

static string BuildBodyExport(IEnumerable<(string File, EmailMessage Message)> messages)
{
    var builder = new StringBuilder();

    foreach (var (file, message) in messages)
    {
        builder.AppendLine($"File: {file}");
        builder.AppendLine($"Subject: {message.Subject}");
        builder.AppendLine($"From: {message.Sender}");
        builder.AppendLine($"To: {string.Join("; ", message.Recipients.To)}");
        if (message.Recipients.Cc.Count > 0)
        {
            builder.AppendLine($"Cc: {string.Join("; ", message.Recipients.Cc)}");
        }
        if (message.Recipients.Bcc.Count > 0)
        {
            builder.AppendLine($"Bcc: {string.Join("; ", message.Recipients.Bcc)}");
        }
        if (message.Timestamp != DateTime.MinValue)
        {
            builder.AppendLine($"Date: {message.Timestamp:O}");
        }
        builder.AppendLine();
        builder.AppendLine(message.BodyText);
        builder.AppendLine("----");
        builder.AppendLine();
    }

    return builder.ToString();
}

static string NormalizeSubject(string subject)
{
    var cleaned = subject ?? string.Empty;
    var pattern = new Regex("^(re|fwd?|aw|sv):\\s*", RegexOptions.IgnoreCase);

    while (pattern.IsMatch(cleaned))
    {
        cleaned = pattern.Replace(cleaned, string.Empty);
    }

    return cleaned.Trim();
}

static DateTime SafeTimestamp(DateTime timestamp)
{
    return timestamp == DateTime.MinValue ? DateTime.MaxValue : timestamp;
}

static string EscapeCsv(string input)
{
    if (input.Contains('"') || input.Contains(',') || input.Contains('\n'))
    {
        return $"\"{input.Replace("\"", "\"\"")}\"";
    }

    return input;
}

record ExtractedFile
{
    public string FileName { get; set; } = string.Empty;
    public List<EmailMessage>? Messages { get; set; }
    public string? Error { get; set; }
}

record EmailMessage
{
    public string Subject { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public RecipientSet Recipients { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public List<AttachmentInfo> Attachments { get; set; } = new();
}

record RecipientSet
{
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
}

record AttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Size { get; set; }
    public string? Base64Data { get; set; }
}

record EmailThread
{
    public string Subject { get; set; } = string.Empty;
    public string NormalizedSubject { get; set; } = string.Empty;
    public List<ThreadMessage> Messages { get; set; } = new();
}

record ThreadMessage
{
    public int Order { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public RecipientSet Recipients { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public List<AttachmentInfo> Attachments { get; set; } = new();
}
