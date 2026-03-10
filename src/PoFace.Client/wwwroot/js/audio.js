(function () {
    'use strict';

    /** @type {AudioContext | null} */
    let audioContext = null;

    function getCtx() {
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
        return audioContext;
    }

    function withSafety(action) {
        try {
            action();
        } catch {
            // Audio is optional. Fail silently by design.
        }
    }

    function envelope(gainNode, now, peak, durationMs) {
        gainNode.gain.setValueAtTime(0.0001, now);
        gainNode.gain.linearRampToValueAtTime(peak, now + 0.01);
        gainNode.gain.exponentialRampToValueAtTime(0.0001, now + durationMs / 1000);
    }

    function playBlip() {
        withSafety(() => {
            const ctx = getCtx();
            const now = ctx.currentTime;
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'square';
            osc.frequency.setValueAtTime(880, now);
            envelope(gain, now, 0.18, 80);

            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start(now);
            osc.stop(now + 0.08);
        });
    }

    function playShutter() {
        withSafety(() => {
            const ctx = getCtx();
            const now = ctx.currentTime;
            const durationSec = 0.12;
            const sampleRate = ctx.sampleRate;
            const frameCount = Math.floor(sampleRate * durationSec);

            const buffer = ctx.createBuffer(1, frameCount, sampleRate);
            const data = buffer.getChannelData(0);
            for (let i = 0; i < frameCount; i++) {
                data[i] = Math.random() * 2 - 1;
            }

            const source = ctx.createBufferSource();
            source.buffer = buffer;
            const gain = ctx.createGain();
            envelope(gain, now, 0.23, 120);

            source.connect(gain);
            gain.connect(ctx.destination);
            source.start(now);
            source.stop(now + durationSec);
        });
    }

    function playSuccessChime() {
        withSafety(() => {
            const ctx = getCtx();
            const now = ctx.currentTime;
            const freqs = [523.25, 659.25, 783.99]; // C5, E5, G5

            for (const f of freqs) {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(f, now);
                envelope(gain, now, 0.10, 600);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.6);
            }
        });
    }

    function vibrateDevice(pattern) {
        withSafety(() => {
            if (navigator.vibrate) {
                navigator.vibrate(pattern);
            }
        });
    }

    window.audioInterop = {
        playBlip,
        playShutter,
        playSuccessChime,
        vibrateDevice
    };
})();
