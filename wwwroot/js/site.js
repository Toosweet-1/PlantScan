document.addEventListener('DOMContentLoaded', function () {

  // --- BACKGROUND SLIDESHOW ---
  const slides = document.querySelectorAll('.bg-slide');
  if (slides.length > 0) {
      let current = 0;
      setInterval(function () {
          slides[current].classList.remove('active');
          current = (current + 1) % slides.length;
          slides[current].classList.add('active');
      }, 5000);
  }

  // --- SCAN PAGE LOGIC ---
  const fileInput = document.getElementById('fileInput');
  if (!fileInput) return;

  const scanZone = document.getElementById('scanZone');
  const uploadInner = document.getElementById('uploadInner');
  const imagePreview = document.getElementById('imagePreview');
  const scanBtn = document.getElementById('scanBtn');
  const cameraBtn = document.getElementById('cameraBtn');
  const cameraSection = document.getElementById('cameraSection');
  const cameraVideo = document.getElementById('cameraVideo');
  const captureBtn = document.getElementById('captureBtn');
  const cameraCanvas = document.getElementById('cameraCanvas');
  const capturedImageInput = document.getElementById('capturedImage');
  const scanForm = document.getElementById('scanForm');

  // Click scan zone to upload
  scanZone.addEventListener('click', function (e) {
      if (imagePreview.style.display === 'block') return;
      fileInput.click();
  });

  // Drag and drop
  scanZone.addEventListener('dragover', function (e) {
      e.preventDefault();
      scanZone.style.borderColor = 'var(--green)';
      scanZone.style.boxShadow = '0 0 60px rgba(0,230,118,0.4)';
  });

  scanZone.addEventListener('dragleave', function () {
      scanZone.style.borderColor = '';
      scanZone.style.boxShadow = '';
  });

  scanZone.addEventListener('drop', function (e) {
      e.preventDefault();
      scanZone.style.borderColor = '';
      scanZone.style.boxShadow = '';
      const file = e.dataTransfer.files[0];
      if (file && file.type.startsWith('image/')) {
          showPreview(file);
      }
  });

  // File selected
  fileInput.addEventListener('change', function () {
      if (fileInput.files && fileInput.files[0]) {
          showPreview(fileInput.files[0]);
      }
  });

  function showPreview(file) {
      const reader = new FileReader();
      reader.onload = function (e) {
          imagePreview.src = e.target.result;
          imagePreview.style.display = 'block';
          uploadInner.style.display = 'none';
          scanBtn.disabled = false;
      };
      reader.readAsDataURL(file);
  }

  // Camera
  let stream = null;

  cameraBtn.addEventListener('click', async function () {
      if (cameraSection.style.display === 'none' || cameraSection.style.display === '') {
          try {
              stream = await navigator.mediaDevices.getUserMedia({
                  video: { facingMode: 'environment' }
              });
              cameraVideo.srcObject = stream;
              cameraSection.style.display = 'flex';
              cameraBtn.textContent = 'Close Camera';
          } catch (err) {
              alert('Camera access denied or unavailable on this device.');
          }
      } else {
          stopCamera();
          cameraSection.style.display = 'none';
          cameraBtn.innerHTML = `
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/>
                  <circle cx="12" cy="13" r="4"/>
              </svg> Take Photo`;
      }
  });

  captureBtn.addEventListener('click', function () {
      cameraCanvas.width = cameraVideo.videoWidth;
      cameraCanvas.height = cameraVideo.videoHeight;
      cameraCanvas.getContext('2d').drawImage(cameraVideo, 0, 0);
      const dataUrl = cameraCanvas.toDataURL('image/jpeg', 0.85);

      // Set hidden input for backend
      capturedImageInput.value = dataUrl;

      // Show preview in scan zone
      imagePreview.src = dataUrl;
      imagePreview.style.display = 'block';
      uploadInner.style.display = 'none';
      scanBtn.disabled = false;

      // Stop camera and hide section
      stopCamera();
      cameraSection.style.display = 'none';
      cameraBtn.innerHTML = `
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/>
              <circle cx="12" cy="13" r="4"/>
          </svg> Take Photo`;
  });

  function stopCamera() {
      if (stream) {
          stream.getTracks().forEach(t => t.stop());
          stream = null;
      }
  }

  // Form submit animation
  scanForm.addEventListener('submit', function () {
      const btnText = document.querySelector('.btn-text');
      const btnLoader = document.querySelector('.btn-loader');
      if (btnText) btnText.style.display = 'none';
      if (btnLoader) btnLoader.style.display = 'inline';
      scanBtn.disabled = true;
  });
});