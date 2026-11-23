package com.didrnrgus.englishspeaking.audio;

import androidx.annotation.OptIn;
import androidx.media3.common.C;
import androidx.media3.common.audio.BaseAudioProcessor;
import androidx.media3.common.util.UnstableApi;

import java.nio.ByteBuffer;

/**
 * 출력 증폭 (모든 소리 x2). 설정 화면의 음량 100% 가 실제로는 원본의 200% 로 나온다.
 *
 * <p>ExoPlayer.setVolume 은 1.0 이 최대라서 디코딩된 PCM 에 직접 곱한다. 노드 음량(0~1)은 그대로
 * player.setVolume 으로 적용하므로 이 프로세서의 배율은 항상 고정이다 (미리 버퍼링된 소리에도 같은 값).
 * 1 을 넘는 부분은 부드럽게 눌러 깨짐(클리핑)을 줄인다.
 *
 * <p>같은 값을 Unity(Scripts/Audio/AudioGainFilter.cs AudioOutput) 와 iOS(EsaAudio.mm kOutputGain) 에서도 쓴다.
 */
@OptIn(markerClass = UnstableApi.class)
final class GainAudioProcessor extends BaseAudioProcessor {
    static final float GAIN = 2f;
    private static final float KNEE = 0.9f;

    @Override
    protected AudioFormat onConfigure(AudioFormat inputAudioFormat) throws UnhandledAudioFormatException {
        // DefaultAudioSink 는 float 출력을 켜지 않으면 16bit 로 바꿔서 넘긴다. 그 밖의 형식은 손대지 않는다.
        if (inputAudioFormat.encoding != C.ENCODING_PCM_16BIT && inputAudioFormat.encoding != C.ENCODING_PCM_FLOAT) {
            return AudioFormat.NOT_SET;
        }
        return inputAudioFormat;
    }

    @Override
    public void queueInput(ByteBuffer inputBuffer) {
        int position = inputBuffer.position();
        int limit = inputBuffer.limit();
        int size = limit - position;
        ByteBuffer out = replaceOutputBuffer(size);
        if (inputAudioFormat.encoding == C.ENCODING_PCM_FLOAT) {
            for (int i = position; i + 3 < limit; i += 4) {
                out.putFloat(softClip(inputBuffer.getFloat(i) * GAIN));
            }
        } else {
            for (int i = position; i + 1 < limit; i += 2) {
                float v = softClip(inputBuffer.getShort(i) / 32768f * GAIN);
                out.putShort((short) Math.max(-32768, Math.min(32767, Math.round(v * 32768f))));
            }
        }
        inputBuffer.position(limit);
        out.flip();
    }

    /** |x| ≤ KNEE 는 그대로, 넘는 부분은 1 에 다가가도록 부드럽게 누른다. */
    static float softClip(float x) {
        float a = Math.abs(x);
        if (a <= KNEE) return x;
        float y = KNEE + (1f - KNEE) * (float) Math.tanh((a - KNEE) / (1f - KNEE));
        return x < 0 ? -y : y;
    }
}
