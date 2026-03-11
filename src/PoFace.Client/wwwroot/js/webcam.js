/**
 * webcam.js — PoFace camera capture module
 * Exposed at window.webcamInterop for .NET JS interop.
 *
 * C# callers (see contracts/blazor-interop.md):
 *   JS.InvokeVoidAsync("webcamInterop.initCamera",   "webcam-preview")
 *   JS.InvokeAsync<string>("webcamInterop.captureFrame", "webcam-preview")
 *   JS.InvokeVoidAsync("webcamInterop.releaseCamera")
 *   JS.InvokeVoidAsync("webcamInterop.flashShutter", "frozen-frame-overlay")
 */

(function () {
    'use strict';

    /** @type {MediaStream | null} */
    let _stream = null;

    /** @type {HTMLCanvasElement | null} */
    let _canvas = null;

    /** @type {CanvasRenderingContext2D | null} */
    let _ctx = null;

    const CANVAS_W = 640;
    const CANVAS_H = 480;
    const QUALITY_NORMAL = 0.92;
    const QUALITY_LOW_BW = 0.75;

    function mapCameraError(error) {
        const name = error?.name ?? '';
        if (name === 'NotAllowedError' || name === 'PermissionDeniedError' || name === 'SecurityError') {
            return 'permission-denied';
        }

        if (name === 'NotFoundError' || name === 'DevicesNotFoundError' || name === 'NotReadableError') {
            return 'camera-unavailable';
        }

        return 'error';
    }

    /**
     * Request camera access and attach the stream to the given video element.
     * @param {string} videoElementId
     */
    async function initCamera(videoElementId) {
        const video = document.getElementById(videoElementId);
        if (!video) return 'error';
        if (!navigator.mediaDevices?.getUserMedia) return 'camera-unavailable';

        try {
            _stream = await navigator.mediaDevices.getUserMedia({ video: true });
            video.srcObject = _stream;

            // Create the shared canvas once.
            if (!_canvas) {
                _canvas = document.createElement('canvas');
                _canvas.width = CANVAS_W;
                _canvas.height = CANVAS_H;
                _canvas.style.display = 'none';
                document.body.appendChild(_canvas);
                _ctx = _canvas.getContext('2d');
            }

            await new Promise((resolve, reject) => {
                video.onplaying = resolve;
                video.onerror = reject;
                video.play().catch(reject);
            });

            return 'ok';
        } catch (error) {
            releaseCamera();
            return mapCameraError(error);
        }
    }

    /**
     * Read the browser camera permission state when available.
     * @returns {Promise<'granted' | 'prompt' | 'denied' | 'unknown' | 'camera-unavailable'>}
     */
    async function getCameraPermissionState() {
        if (!navigator.mediaDevices?.getUserMedia) return 'camera-unavailable';
        if (!navigator.permissions?.query) return 'unknown';

        try {
            const status = await navigator.permissions.query({ name: 'camera' });
            return status?.state ?? 'unknown';
        } catch {
            return 'unknown';
        }
    }

    /**
     * Draw the current video frame to the hidden canvas, encode as JPEG, and
     * return the Base64 data URL.
     * Quality adapts to network conditions: 0.60 on slow connections, 0.85 otherwise.
     * @param {string} videoElementId
     * @returns {Promise<string>} e.g. "data:image/jpeg;base64,/9j/4AAQ..."
     */
    async function captureFrame(videoElementId) {
        const video = document.getElementById(videoElementId);
        if (!video) throw new Error(`Video element #${videoElementId} not found.`);
        if (!_canvas || !_ctx) throw new Error('Camera not initialized. Call initCamera first.');

        _ctx.drawImage(video, 0, 0, CANVAS_W, CANVAS_H);

        // Bitrate adaptation (navigator.connection is available in Chromium-based browsers).
        const downlink = navigator.connection?.downlink ?? Infinity;
        const quality  = downlink < 1.0 ? QUALITY_LOW_BW : QUALITY_NORMAL;

        return _canvas.toDataURL('image/jpeg', quality);
    }

    /**
     * Stop all tracks on the stored MediaStream to release the camera hardware.
     */
    function releaseCamera() {
        if (_stream) {
            _stream.getTracks().forEach(t => t.stop());
            _stream = null;
        }
        if (_canvas) {
            _canvas.remove();
            _canvas = null;
            _ctx    = null;
        }
    }

    /**
     * Apply the white-flash CSS animation to the overlay element.
     * The element is expected to have a CSS class that defines the `shutter-flash` keyframe.
     * @param {string} overlayElementId
     */
    async function flashShutter(overlayElementId) {
        const overlay = document.getElementById(overlayElementId);
        if (!overlay) return;

        overlay.classList.add('shutter-flash');
        await new Promise(resolve => setTimeout(resolve, 160)); // matches CSS animation duration
        overlay.classList.remove('shutter-flash');
    }

    // Assign to window so .NET JS interop can resolve the namespace synchronously.
    window.webcamInterop = {
        initCamera,
        getCameraPermissionState,
        captureFrame,
        releaseCamera,
        flashShutter
    };
})();
