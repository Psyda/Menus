/* The Pinecone Kitchen — menu board runtime.
   1. Scales the fixed 1920×1080 stage to fit any screen.
   2. Swaps in a branded placeholder for any dish photo that fails to load.
   3. Reloads the page when a new version is published. */
(function () {
  var W = 1920, H = 1080;
  var stage = document.querySelector('.stage');

  function fit() {
    var s = Math.min(window.innerWidth / W, window.innerHeight / H);
    stage.style.transform = 'translate(-50%, -50%) scale(' + s + ')';
  }
  window.addEventListener('resize', fit);
  fit();

  var imgs = document.querySelectorAll('.media img');
  for (var i = 0; i < imgs.length; i++) {
    (function (img) {
      function fail() {
        img.parentNode.classList.add('no-img');
        img.style.display = 'none';
      }
      if (img.complete && img.naturalWidth === 0) fail();
      else img.addEventListener('error', fail);
    })(imgs[i]);
  }

  // Auto-update: TVs sit on this page for days. Every minute, fetch a fresh
  // copy of the page and reload if its HTML has changed. Edits to menu.css,
  // menu.js or a photo don't change the HTML, so bump ?v= in the pages too.
  var POLL_MS = 60 * 1000;
  var loaded = null;
  function check() {
    if (navigator.onLine === false) return;
    fetch(location.href, { cache: 'no-store' })
      .then(function (r) { return r.ok ? r.text() : null; })
      .then(function (html) {
        // Ignore errors and non-menu responses (deploy hiccups, wifi portals).
        if (!html || html.indexOf('class="stage') === -1) return;
        if (loaded === null) loaded = html;
        else if (html !== loaded) location.reload();
      })
      .catch(function () {});
  }
  if (/^https?:$/.test(location.protocol)) {
    check();
    setInterval(check, POLL_MS);
  }
})();
