package com.didrnrgus.englishspeaking.audio;

import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

import androidx.annotation.Nullable;
import androidx.annotation.OptIn;
import androidx.media3.common.AudioAttributes;
import androidx.media3.common.C;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.session.DefaultMediaNotificationProvider;
import androidx.media3.session.MediaSession;
import androidx.media3.session.MediaSessionService;

/**
 * 화면이 꺼져도 재생을 이어가는 포그라운드 미디어 서비스.
 *
 * <p>Media3 가 재생 중일 때 알림(잠금화면·알림창 컨트롤)과 포그라운드 상태를 관리한다.
 * 문장 사이 '텀' 도 무음 트랙으로 재생하므로 시퀀스가 끝날 때까지 플레이어는 계속 '재생 중' 상태이고,
 * 그동안 서비스가 포그라운드로 유지된다.
 */
@OptIn(markerClass = UnstableApi.class)
public class PlaybackService extends MediaSessionService {
    @Nullable
    static PlaybackService instance;

    private ExoPlayer player;
    private MediaSession session;
    private SequenceEngine engine;

    @Override
    public void onCreate() {
        super.onCreate();
        AudioAttributes attributes = new AudioAttributes.Builder()
                .setUsage(C.USAGE_MEDIA)
                .setContentType(C.AUDIO_CONTENT_TYPE_SPEECH)
                .build();
        player = new ExoPlayer.Builder(this)
                .setAudioAttributes(attributes, /* handleAudioFocus= */ true)
                .setHandleAudioBecomingNoisy(true) // 이어폰 분리 시 일시정지
                .setWakeMode(C.WAKE_MODE_LOCAL) // 재생(무음 텀 포함) 중 CPU 유지
                .build();
        engine = new SequenceEngine(this, player);

        MediaSession.Builder builder = new MediaSession.Builder(this, engine.sessionPlayer());
        PendingIntent launch = launchIntent();
        if (launch != null) builder.setSessionActivity(launch);
        session = builder.build();

        instance = this;
        AudioBridge.onServiceReady(this);
    }

    SequenceEngine engine() {
        return engine;
    }

    @Nullable
    @Override
    public MediaSession onGetSession(MediaSession.ControllerInfo controllerInfo) {
        return session;
    }

    @Override
    public void onTaskRemoved(@Nullable Intent rootIntent) {
        // 앱을 최근 목록에서 지웠을 때. Unity 가 곧 프로세스를 죽이므로 재생 중이어도 이어갈 수 없다 → 바로 정리.
        // (보통은 AudioBridge 가 액티비티 onDestroy 직전에 먼저 정리한다. 여기는 Android 9 이하용 보조 경로.)
        shutdownNow();
    }

    /**
     * 앱(Unity)이 닫힐 때: 재생을 멈추고 미디어 세션·알림을 그 자리에서 지운다.
     *
     * <p>Unity 는 액티비티 onDestroy 에서 {@code Process.killProcess} 로 프로세스를 바로 끝내므로
     * Media3 의 비동기 알림 정리나 {@link #onDestroy()} 는 실행되지 못한다. 그대로 두면 일시정지 상태의
     * 알림이 주인 없이 남아 재부팅 전까지 지워지지 않는다. 그래서 여기서는 모두 동기로 처리한다.
     */
    void shutdownNow() {
        if (engine != null) engine.stop();
        if (session != null) {
            session.release(); // 시스템 미디어 세션 즉시 해제 → 알림창·잠금화면 플레이어 제거
            session = null;
        }
        if (Build.VERSION.SDK_INT >= 24) {
            stopForeground(STOP_FOREGROUND_REMOVE);
        } else {
            stopForeground(true);
        }
        cancelNotification(this);
        AudioBridge.releaseController(); // 같은 프로세스 컨트롤러가 바인드돼 있으면 서비스가 안 내려간다
        stopSelf(); // 시작된 서비스로 남아 있으면 프로세스가 죽은 뒤 시스템이 빈 서비스를 다시 띄운다
    }

    /** Media3 기본 알림(id 1001) 제거. 이전 실행에서 남은 알림 정리에도 쓴다. */
    static void cancelNotification(Context context) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager != null) manager.cancel(DefaultMediaNotificationProvider.DEFAULT_NOTIFICATION_ID);
    }

    @Override
    public void onDestroy() {
        instance = null;
        if (engine != null) engine.release();
        if (session != null) session.release();
        if (player != null) player.release();
        session = null;
        player = null;
        engine = null;
        AudioBridge.releaseController();
        super.onDestroy();
    }

    @Nullable
    private PendingIntent launchIntent() {
        Intent intent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (intent == null) return null;
        intent.addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        return PendingIntent.getActivity(this, 0, intent,
                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }
}
