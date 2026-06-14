window.gameSounds = {
    audioContext: null,

    play(soundName) {
        const context = this.getAudioContext();

        if (!context) {
            return;
        }

        if (context.state === "suspended") {
            context.resume();
        }

        switch (soundName) {
            case "start":
                this.playNotes(context, [523, 659], 0.08, "sine", 0.08);
                break;
            case "dice":
                this.playClickRoll(context);
                break;
            case "correct":
                this.playNotes(context, [523, 659, 784], 0.09, "triangle", 0.08);
                break;
            case "wrong":
                this.playNotes(context, [220, 165], 0.12, "sine", 0.07);
                break;
            case "timeUp":
                this.playNotes(context, [440, 440], 0.1, "square", 0.05);
                break;
            case "move":
                this.playTone(context, 660, 0, 0.04, "square", 0.045);
                break;
            case "winner":
                this.playNotes(context, [523, 659, 784, 1046], 0.1, "triangle", 0.09);
                break;
        }
    },

    getAudioContext() {
        if (!window.AudioContext && !window.webkitAudioContext) {
            return null;
        }

        if (!this.audioContext) {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        return this.audioContext;
    },

    playClickRoll(context) {
        for (let index = 0; index < 5; index++) {
            const frequency = 420 + Math.random() * 360;
            this.playTone(context, frequency, index * 0.045, 0.035, "square", 0.04);
        }
    },

    playNotes(context, frequencies, duration, waveType, volume) {
        frequencies.forEach((frequency, index) => {
            this.playTone(context, frequency, index * duration, duration, waveType, volume);
        });
    },

    playTone(context, frequency, delay, duration, waveType, volume) {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        const startTime = context.currentTime + delay;
        const endTime = startTime + duration;

        oscillator.type = waveType;
        oscillator.frequency.setValueAtTime(frequency, startTime);

        gain.gain.setValueAtTime(0.0001, startTime);
        gain.gain.exponentialRampToValueAtTime(volume, startTime + 0.01);
        gain.gain.exponentialRampToValueAtTime(0.0001, endTime);

        oscillator.connect(gain);
        gain.connect(context.destination);

        oscillator.start(startTime);
        oscillator.stop(endTime + 0.02);
    }
};
