package com.didrnrgus.englishspeaking.audio;

import android.content.ComponentName;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import androidx.media3.session.MediaController;
import androidx.media3.session.SessionToken;

import com.google.common.util.concurrent.ListenableFuture;
import com.google.common.util.concurrent.MoreExecutors;

import java.util.ArrayList;

/**
 * Unity(C#) 가 호출하는 정적 API.
 *
 * <p>Unity 는 자기 스레드에서 호출하므로 모든 명령을 메인 스레드로 넘겨 {@link PlaybackService} 의
 * {@link SequenceEngine} 에 전달한다. 서비스가 아직 없으면 MediaController 로 서비스를 바인드해 띄우고,
 * 명령은 서비스가 준비될 때까지 대기열에 둔다.
 *
 * <p>재생 상태는 {@link #getState()} 로 조회한다 (Unity 가 주기적으로 폴링 — 화면이 꺼진 동안 멈춰 있던 Unity 도
 * 복귀하자마자 현재 상태를 받는다).
 */
public final class AudioBridge {
    private static final String TAG = "EsaAudio";

    interface ServiceAction {
        void run(PlaybackService service);
    }

    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static final ArrayList<ServiceAction> PENDING = new ArrayList<>();
    private static Context appContext;
    private static ListenableFuture<MediaController> controllerFuture;

    /** 엔진이 바뀔 때마다 갱신하는 상태 JSON (Unity 스레드에서 읽음). */
    static volatile String stateJson = "{\"id\":0,\"active\":false,\"playing\":false,\"ended\":false,\"step\":-1,\"gap\":false}";

    private AudioBridge() {
    }

    /** Unity 시작 시 1회 (다시 불러도 무해). */
    public static void init(final Context context) {
        if (context == null) return;
        appContext = context.getApplicationContext();
        MAIN.post(AudioBridge::connect);
    }

    /** 시퀀스 재생. JSON 형식은 SequenceEngine#load 참고. */
    public static void play(final String json) {
        run(service -> service.engine().load(json));
    }

    public static void pause() {
        run(service -> service.engine().pause());
    }

    public static void resume() {
        run(service -> service.engine().resume());
    }

    public static void stop() {
        run(service -> service.engine().stop());
    }

    /** 시퀀스 안의 step 으로 이동하고 재생한다. */
    public static void seekStep(final int step) {
        run(service -> service.engine().seekToStep(step, true));
    }

    public static void setRepeat(final boolean repeat) {
        run(service -> service.engine().setRepeat(repeat));
    }

    /** 듣기 시퀀스 노드의 음량·빠르기·텀을 바꾼다. 재생 중인 소리에 바로 적용된다. */
    public static void updateNode(final int index, final float volume, final float speed, final float gapSec) {
        run(service -> service.engine().updateNode(index, volume, speed, gapSec));
    }

    public static String getState() {
        return stateJson;
    }

    // ---------------------------------------------------------------------------------------------

    private static void run(final ServiceAction action) {
        MAIN.post(() -> {
            PlaybackService service = PlaybackService.instance;
            if (service != null) {
                safeRun(action, service);
            } else {
                PENDING.add(action);
                connect();
            }
        });
    }

    private static void safeRun(ServiceAction action, PlaybackService service) {
        try {
            action.run(service);
        } catch (Throwable t) {
            Log.e(TAG, "command failed", t);
        }
    }

    /** 메인 스레드. 서비스가 만들어지면 대기 중인 명령을 실행한다. */
    static void onServiceReady(PlaybackService service) {
        ArrayList<ServiceAction> actions = new ArrayList<>(PENDING);
        PENDING.clear();
        for (ServiceAction action : actions) safeRun(action, service);
    }

    /** 메인 스레드. 서비스가 종료되면 다음 명령 때 다시 연결한다. */
    static void onServiceDestroyed() {
        if (controllerFuture != null) {
            MediaController.releaseFuture(controllerFuture);
            controllerFuture = null;
        }
    }

    private static void connect() {
        if (appContext == null || controllerFuture != null) return;
        SessionToken token = new SessionToken(appContext, new ComponentName(appContext, PlaybackService.class));
        final ListenableFuture<MediaController> future = new MediaController.Builder(appContext, token).buildAsync();
        controllerFuture = future;
        future.addListener(() -> {
            try {
                future.get();
            } catch (Exception e) {
                Log.e(TAG, "controller connect failed", e);
                if (controllerFuture == future) controllerFuture = null;
            }
        }, MoreExecutors.directExecutor());
    }
}
