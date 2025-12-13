const form = document.getElementById("extract-form");
const errorBox = document.getElementById("error");
const resultsCard = document.getElementById("results-card");
const resultsList = document.getElementById("results");
const resultCount = document.getElementById("result-count");
const copyAllBtn = document.getElementById("copy-all");
const template = document.getElementById("post-row");

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  errorBox.textContent = "";
  resultsCard.hidden = true;
  resultsList.innerHTML = "";
  toggleLoading(true);

  const profileUrl = document.getElementById("profileUrl").value.trim();
  const includeDescriptions = document.getElementById("includeDescriptions").checked;
  const oldestFirst = document.getElementById("oldestFirst").checked;

  try {
    const response = await fetch("/api/extract", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ profileUrl, includeDescriptions, newestFirst: !oldestFirst }),
    });

    const payload = await response.json();

    if (!response.ok) {
      throw new Error(payload.error || payload.title || "Unable to fetch posts.");
    }

    renderResults(payload.posts || []);
  } catch (err) {
    errorBox.textContent = err.message;
  } finally {
    toggleLoading(false);
  }
});

copyAllBtn.addEventListener("click", () => {
  const urls = Array.from(document.querySelectorAll(".post-link"))
    .map((link) => link.href)
    .join("\n");

  copyToClipboard(urls, "Copied all post URLs to your clipboard.");
});

function renderResults(posts) {
  resultCount.textContent = `${posts.length} post${posts.length === 1 ? "" : "s"} found`;
  resultsCard.hidden = false;

  if (posts.length === 0) {
    resultsList.innerHTML = '<p class="hint">No posts could be detected for that profile.</p>';
    return;
  }

  posts.forEach((post, index) => {
    const fragment = template.content.cloneNode(true);
    const badge = fragment.querySelector(".badge");
    const link = fragment.querySelector(".post-link");
    const description = fragment.querySelector(".description");
    const copyUrl = fragment.querySelector(".copy-url");
    const copyDescription = fragment.querySelector(".copy-description");

    badge.textContent = index + 1;
    link.href = post.url;
    link.textContent = post.url;
    description.textContent = post.description || "";

    copyUrl.addEventListener("click", () => copyToClipboard(post.url, "URL copied."));

    if (post.description) {
      copyDescription.hidden = false;
      copyDescription.addEventListener("click", () =>
        copyToClipboard(post.description, "Description copied."),
      );
    }

    resultsList.appendChild(fragment);
  });
}

function toggleLoading(isLoading) {
  const submitBtn = document.getElementById("submit-btn");
  submitBtn.textContent = isLoading ? "Extracting..." : "Extract posts";
  submitBtn.classList.toggle("loading", isLoading);
}

async function copyToClipboard(text, successMessage) {
  if (!text) return;

  try {
    await navigator.clipboard.writeText(text);
    showToast(successMessage);
  } catch {
    showToast("Unable to copy. Please copy manually.");
  }
}

function showToast(message) {
  const toast = document.createElement("div");
  toast.textContent = message;
  toast.className = "toast";
  document.body.appendChild(toast);
  requestAnimationFrame(() => toast.classList.add("visible"));
  setTimeout(() => toast.remove(), 2500);
}
