/* The Pinecone Kitchen — menu board runtime.
   1. Scales the fixed 1920×1080 stage to fit any screen.
   2. Swaps in a branded placeholder for any dish photo that fails to load. */
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
})();
