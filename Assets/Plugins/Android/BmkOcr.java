package com.bmk.ocr;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;

import com.google.android.gms.tasks.OnFailureListener;
import com.google.android.gms.tasks.OnSuccessListener;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.text.Text;
import com.google.mlkit.vision.text.TextRecognition;
import com.google.mlkit.vision.text.TextRecognizer;
import com.google.mlkit.vision.text.latin.TextRecognizerOptions;
import com.unity3d.player.UnityPlayer;

/**
 * Texterkennung mit Google ML Kit (on-device, offline).
 * Wird aus Unity (BmkScanner.cs) aufgerufen; das Ergebnis geht per UnitySendMessage zurück.
 * Ablage in Unity: Assets/Plugins/Android/BmkOcr.java
 */
public class BmkOcr {

    private static TextRecognizer recognizer;

    public static void recognize(byte[] jpeg, final String gameObjectName, final String callbackMethod) {
        try {
            if (recognizer == null) {
                recognizer = TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS);
            }

            Bitmap bitmap = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.length);
            if (bitmap == null) {
                send(gameObjectName, callbackMethod, "");
                return;
            }

            InputImage image = InputImage.fromBitmap(bitmap, 0); // Bild ist in Unity schon aufrecht gedreht

            recognizer.process(image)
                    .addOnSuccessListener(new OnSuccessListener<Text>() {
                        @Override
                        public void onSuccess(Text result) {
                            send(gameObjectName, callbackMethod, result.getText());
                        }
                    })
                    .addOnFailureListener(new OnFailureListener() {
                        @Override
                        public void onFailure(Exception e) {
                            send(gameObjectName, callbackMethod, "");
                        }
                    });
        } catch (Exception e) {
            send(gameObjectName, callbackMethod, "");
        }
    }

    private static void send(String gameObjectName, String method, String message) {
        UnityPlayer.UnitySendMessage(gameObjectName, method, message == null ? "" : message);
    }
}
