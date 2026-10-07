package com.didrnrgus.englishspeaking.audio;

import android.content.Context;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;

import androidx.annotation.Nullable;
import androidx.annotation.OptIn;
import androidx.media3.common.ForwardingPlayer;
import androidx.media3.common.MediaItem;
import androidx.media3.common.MediaMetadata;
import androidx.media3.common.PlaybackException;
import androidx.media3.common.PlaybackParameters;
import androidx.media3.common.Player;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.exoplayer.ExoPlayer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;

/**
 * 듣기 시퀀스 재생기.
 *
 * <p>Unity 가 펼쳐서 보낸 step 목록(파일 + 노드 번호)을 ExoPlayer 플레이리스트로 만든다.
 * step 마다 [음성 클립, 무음 텀] 두 항목을 넣는다 (플레이리스트 index = step * 2 (+1)).
 * <ul>
 *   <li>음량·빠르기는 노드 단위 값이고, 클립이 시작될 때 / 값이 바뀌는 즉시 플레이어에 적용한다.</li>
 *   <li>텀은 긴 무음 파일을 재생하면서 타이머로 끊는다 → 텀을 바꾸면 진행 중인 텀에도 바로 반영되고,
 *       그동안 플레이어가 '재생 중' 이라 화면이 꺼져도 포그라운드 서비스와 wake lock 이 유지된다.</li>
 *   <li>빠르기 변경은 다음 클립의 값으로 텀이 시작될 때 미리 바꿔 둔다 (오디오 버퍼 지연을 무음이 흡수).</li>
 * </ul>
 */
@OptIn(markerClass = UnstableApi.class)
final class SequenceEngine implements Player.Listener {
    private static final String TAG = "EsaAudio";
    /** 무음 파일 길이. 텀 최대 5초 × 빠르기 최대 2배보다 넉넉하게. */
    private static final int SILENCE_SECONDS = 20;
    private static final int SILENCE_SAMPLE_RATE = 8000;

    static final class Node {
        float volume = 1f;
        float speed = 1f;
        float gap = 1f;
    }

    static final class Step {
        String path;
        int node;
        int page;
        String title;
        String subtitle;
    }

    private final Context context;
    private final ExoPlayer player;
    private final Player sessionPlayer;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable gapFinished = this::onGapFinished;

    private Node[] nodes = new Node[0];
    private final ArrayList<Step> steps = new ArrayList<>();
    private int requestId;
    private boolean repeat;
    private String title = "";

    private boolean active;
    private boolean ended;
    private int currentItem = -1;

    // 텀(무음) 타이머 — 일시정지 동안은 멈춘다
    private boolean inGap;
    private long gapElapsedMs;
    private long gapResumedAt = -1;

    @Nullable
    private File silenceFile;

    SequenceEngine(Context context, ExoPlayer player) {
        this.context = context;
        this.player = player;
        this.sessionPlayer = new SessionPlayer(player);
        player.addListener(this);
        publish();
    }

    Player sessionPlayer() {
        return sessionPlayer;
    }

    void release() {
        handler.removeCallbacks(gapFinished);
        player.removeListener(this);
    }

    // ---------------------------------------------------------------------------------------------
    // 명령

    /**
     * <pre>
     * {"id":3, "title":"S2-U01 여행 첫걸음", "repeat":false, "startStep":0,
     *  "nodes":[{"volume":1.0,"speed":1.0,"gap":1.0}, ...],
     *  "steps":[{"path":"/data/.../S2-U01-01.mp3","node":0,"page":0,"title":"I'm from Korea.","subtitle":"..."}, ...]}
     * </pre>
     */
    void load(String json) {
        try {
            JSONObject root = new JSONObject(json);
            JSONArray nodeArray = root.getJSONArray("nodes");
            JSONArray stepArray = root.getJSONArray("steps");
            Node[] newNodes = new Node[nodeArray.length()];
            for (int i = 0; i < newNodes.length; i++) {
                JSONObject o = nodeArray.getJSONObject(i);
                Node n = new Node();
                n.volume = clamp((float) o.optDouble("volume", 1), 0f, 1f);
                n.speed = clamp((float) o.optDouble("speed", 1), 0.25f, 4f);
                n.gap = clamp((float) o.optDouble("gap", 1), 0f, 10f);
                newNodes[i] = n;
            }
            ArrayList<Step> newSteps = new ArrayList<>();
            for (int i = 0; i < stepArray.length(); i++) {
                JSONObject o = stepArray.getJSONObject(i);
                Step s = new Step();
                s.path = o.getString("path");
                s.node = o.optInt("node", 0);
                s.page = o.optInt("page", 0);
                s.title = o.optString("title", "");
                s.subtitle = o.optString("subtitle", "");
                if (s.node < 0 || s.node >= newNodes.length) s.node = 0;
                newSteps.add(s);
            }
            if (newSteps.isEmpty() || newNodes.length == 0) {
                stop();
                return;
            }

            cancelGap();
            nodes = newNodes;
            steps.clear();
            steps.addAll(newSteps);
            requestId = root.optInt("id", requestId + 1);
            repeat = root.optBoolean("repeat", false);
            title = root.optString("title", "");
            int start = Math.max(0, Math.min(root.optInt("startStep", 0), steps.size() - 1));

            File silence = silenceFile();
            ArrayList<MediaItem> items = new ArrayList<>(steps.size() * 2);
            for (int i = 0; i < steps.size(); i++) {
                Step s = steps.get(i);
                MediaMetadata metadata = new MediaMetadata.Builder()
                        .setTitle(s.title)
                        .setDisplayTitle(s.title)
                        .setSubtitle(s.subtitle)
                        .setArtist(title)
                        .build();
                items.add(new MediaItem.Builder()
                        .setMediaId("c" + i)
                        .setUri(Uri.fromFile(new File(s.path)))
                        .setMediaMetadata(metadata)
                        .build());
                items.add(new MediaItem.Builder()
                        .setMediaId("g" + i)
                        .setUri(Uri.fromFile(silence))
                        .setMediaMetadata(metadata)
                        .build());
            }

            active = true;
            ended = false;
            currentItem = -1;
            player.setRepeatMode(Player.REPEAT_MODE_OFF);
            player.setMediaItems(items, start * 2, 0);
            onItem(start * 2);
            player.prepare();
            player.play();
        } catch (Exception e) {
            Log.e(TAG, "load failed", e);
            stop();
        }
        publish();
    }

    void pause() {
        player.pause();
        publish();
    }

    void resume() {
        if (steps.isEmpty()) return;
        if (ended || !active) {
            seekToStep(0, true);
            return;
        }
        if (player.getPlaybackState() == Player.STATE_IDLE) player.prepare();
        player.play();
        publish();
    }

    void stop() {
        cancelGap();
        active = false;
        ended = false;
        currentItem = -1;
        player.stop();
        player.clearMediaItems();
        publish();
    }

    void seekToStep(int step, boolean play) {
        if (steps.isEmpty()) return;
        step = Math.max(0, Math.min(step, steps.size() - 1));
        cancelGap();
        active = true;
        ended = false;
        player.seekTo(step * 2, 0);
        onItem(step * 2);
        if (player.getPlaybackState() == Player.STATE_IDLE) player.prepare();
        if (play) player.play();
        publish();
    }

    void setRepeat(boolean value) {
        repeat = value;
        publish();
    }

    void updateNode(int index, float volume, float speed, float gapSec) {
        if (index < 0 || index >= nodes.length) return;
        Node n = nodes[index];
        n.volume = clamp(volume, 0f, 1f);
        n.speed = clamp(speed, 0.25f, 4f);
        n.gap = clamp(gapSec, 0f, 10f);
        if (!active || currentItem < 0) return;

        int step = currentItem / 2;
        Step current = steps.get(step);
        if (!inGap) {
            if (current.node == index) {
                player.setVolume(n.volume);
                setSpeed(n.speed);
            }
        } else {
            if (current.node == index) scheduleGap(); // 남은 텀을 새 값으로 다시 계산
            int next = nextStep(step);
            if (next >= 0 && steps.get(next).node == index) setSpeed(n.speed);
        }
    }

    /** 알림·잠금화면의 다음 버튼: 다음 예문(페이지)의 첫 step. */
    void nextPage() {
        if (steps.isEmpty()) return;
        int step = Math.max(0, currentItem / 2);
        int page = steps.get(step).page;
        for (int i = step + 1; i < steps.size(); i++) {
            if (steps.get(i).page != page) {
                seekToStep(i, true);
                return;
            }
        }
        if (repeat) seekToStep(0, true);
    }

    /** 알림·잠금화면의 이전 버튼: 이전 예문(페이지)의 첫 step. */
    void prevPage() {
        if (steps.isEmpty()) return;
        int step = Math.max(0, currentItem / 2);
        int first = firstStepOfPage(steps.get(step).page);
        if (first > 0) {
            seekToStep(firstStepOfPage(steps.get(first - 1).page), true);
        } else {
            seekToStep(0, true);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Player.Listener

    @Override
    public void onMediaItemTransition(@Nullable MediaItem mediaItem, int reason) {
        if (!active) return;
        onItem(player.getCurrentMediaItemIndex());
        publish();
    }

    @Override
    public void onPlayWhenReadyChanged(boolean playWhenReady, int reason) {
        if (inGap) {
            if (playWhenReady) resumeGapTimer();
            else pauseGapTimer();
        }
        publish();
    }

    @Override
    public void onPlaybackStateChanged(int state) {
        if (state == Player.STATE_ENDED && active) {
            onGapFinished();
        }
        publish();
    }

    @Override
    public void onPlayerError(PlaybackException error) {
        Log.e(TAG, "player error at item " + player.getCurrentMediaItemIndex(), error);
        // 파일 하나가 깨졌으면 그 step 을 건너뛴다
        if (!active || steps.isEmpty()) return;
        int step = Math.max(0, player.getCurrentMediaItemIndex() / 2);
        int next = nextStep(step);
        if (next >= 0 && next != step) {
            player.prepare();
            seekToStep(next, true);
        } else {
            finish();
        }
    }

    // ---------------------------------------------------------------------------------------------

    /** 플레이리스트 index 가 바뀌었을 때: 클립이면 노드 값 적용, 텀이면 타이머 시작. */
    private void onItem(int index) {
        if (index < 0 || index >= steps.size() * 2) return;
        if (index == currentItem) return;
        currentItem = index;
        int step = index / 2;
        cancelGap();
        if (index % 2 == 0) {
            Node n = nodes[steps.get(step).node];
            player.setVolume(n.volume);
            setSpeed(n.speed);
        } else {
            int next = nextStep(step);
            if (next >= 0) setSpeed(nodes[steps.get(next).node].speed);
            inGap = true;
            gapElapsedMs = 0;
            gapResumedAt = -1;
            if (player.getPlayWhenReady()) resumeGapTimer();
        }
    }

    private void onGapFinished() {
        handler.removeCallbacks(gapFinished);
        if (!active || steps.isEmpty()) return;
        int step = Math.max(0, currentItem / 2);
        int next = nextStep(step);
        if (next >= 0) {
            seekToStep(next, true);
        } else {
            finish();
        }
    }

    private void finish() {
        cancelGap();
        active = false;
        ended = true;
        player.pause();
        publish();
    }

    private int nextStep(int step) {
        if (step + 1 < steps.size()) return step + 1;
        return repeat ? 0 : -1;
    }

    private int firstStepOfPage(int page) {
        for (int i = 0; i < steps.size(); i++) {
            if (steps.get(i).page == page) return i;
        }
        return 0;
    }

    private void setSpeed(float speed) {
        if (player.getPlaybackParameters().speed != speed) {
            player.setPlaybackParameters(new PlaybackParameters(speed));
        }
    }

    private float currentGapSeconds() {
        if (currentItem < 0 || steps.isEmpty()) return 0f;
        return nodes[steps.get(currentItem / 2).node].gap;
    }

    private void resumeGapTimer() {
        if (gapResumedAt < 0) gapResumedAt = SystemClock.elapsedRealtime();
        scheduleGap();
    }

    private void pauseGapTimer() {
        if (gapResumedAt >= 0) {
            gapElapsedMs += SystemClock.elapsedRealtime() - gapResumedAt;
            gapResumedAt = -1;
        }
        handler.removeCallbacks(gapFinished);
    }

    /** 현재 텀 값 기준으로 남은 시간만큼 타이머를 다시 건다. */
    private void scheduleGap() {
        handler.removeCallbacks(gapFinished);
        if (!inGap || gapResumedAt < 0) return;
        long elapsed = gapElapsedMs + (SystemClock.elapsedRealtime() - gapResumedAt);
        long remaining = Math.max(0, (long) (currentGapSeconds() * 1000f) - elapsed);
        handler.postDelayed(gapFinished, remaining);
    }

    private void cancelGap() {
        handler.removeCallbacks(gapFinished);
        inGap = false;
        gapElapsedMs = 0;
        gapResumedAt = -1;
    }

    private void publish() {
        int step = currentItem >= 0 ? currentItem / 2 : -1;
        boolean playing = active && player.getPlayWhenReady();
        AudioBridge.stateJson = "{\"id\":" + requestId
                + ",\"active\":" + active
                + ",\"playing\":" + playing
                + ",\"ended\":" + ended
                + ",\"step\":" + step
                + ",\"gap\":" + inGap
                + ",\"repeat\":" + repeat + "}";
    }

    private File silenceFile() throws IOException {
        if (silenceFile != null && silenceFile.exists()) return silenceFile;
        File file = new File(context.getCacheDir(), "esa_silence_" + SILENCE_SECONDS + "s.wav");
        if (!file.exists() || file.length() == 0) writeSilenceWav(file);
        silenceFile = file;
        return file;
    }

    /** 8kHz 16bit 모노 무음 WAV. */
    private static void writeSilenceWav(File file) throws IOException {
        int dataSize = SILENCE_SAMPLE_RATE * 2 * SILENCE_SECONDS;
        byte[] header = new byte[44];
        writeAscii(header, 0, "RIFF");
        writeInt(header, 4, 36 + dataSize);
        writeAscii(header, 8, "WAVE");
        writeAscii(header, 12, "fmt ");
        writeInt(header, 16, 16); // fmt chunk size
        writeShort(header, 20, 1); // PCM
        writeShort(header, 22, 1); // mono
        writeInt(header, 24, SILENCE_SAMPLE_RATE);
        writeInt(header, 28, SILENCE_SAMPLE_RATE * 2); // byte rate
        writeShort(header, 32, 2); // block align
        writeShort(header, 34, 16); // bits per sample
        writeAscii(header, 36, "data");
        writeInt(header, 40, dataSize);
        File tmp = new File(file.getPath() + ".tmp");
        try (FileOutputStream out = new FileOutputStream(tmp)) {
            out.write(header);
            out.write(new byte[dataSize]);
        }
        if (!tmp.renameTo(file)) throw new IOException("rename failed: " + file);
    }

    private static void writeAscii(byte[] b, int at, String s) {
        for (int i = 0; i < s.length(); i++) b[at + i] = (byte) s.charAt(i);
    }

    private static void writeInt(byte[] b, int at, int v) {
        b[at] = (byte) v;
        b[at + 1] = (byte) (v >> 8);
        b[at + 2] = (byte) (v >> 16);
        b[at + 3] = (byte) (v >> 24);
    }

    private static void writeShort(byte[] b, int at, int v) {
        b[at] = (byte) v;
        b[at + 1] = (byte) (v >> 8);
    }

    private static float clamp(float v, float min, float max) {
        if (Float.isNaN(v)) return min;
        return Math.max(min, Math.min(max, v));
    }

    // ---------------------------------------------------------------------------------------------

    /**
     * 알림·잠금화면·블루투스 버튼이 보는 플레이어.
     * 이전/다음 = 예문(페이지) 단위 이동, 끝난 뒤 재생 = 처음부터 다시.
     */
    private final class SessionPlayer extends ForwardingPlayer {
        SessionPlayer(Player player) {
            super(player);
        }

        @Override
        public void play() {
            resume();
        }

        @Override
        public void pause() {
            SequenceEngine.this.pause();
        }

        @Override
        public void seekToNext() {
            nextPage();
        }

        @Override
        public void seekToNextMediaItem() {
            nextPage();
        }

        @Override
        public void seekToPrevious() {
            prevPage();
        }

        @Override
        public void seekToPreviousMediaItem() {
            prevPage();
        }

        @Override
        public Commands getAvailableCommands() {
            return super.getAvailableCommands().buildUpon()
                    .add(COMMAND_SEEK_TO_NEXT)
                    .add(COMMAND_SEEK_TO_NEXT_MEDIA_ITEM)
                    .add(COMMAND_SEEK_TO_PREVIOUS)
                    .add(COMMAND_SEEK_TO_PREVIOUS_MEDIA_ITEM)
                    .build();
        }

        @Override
        public boolean isCommandAvailable(int command) {
            return getAvailableCommands().contains(command);
        }
    }
}
