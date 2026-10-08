// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.Paraform
{
public static class FormAssetGenerator
{
    public readonly ref struct GenerationPrep
    {
        public readonly Vector2Int contentSize;
        public readonly Vector2Int targetSize;

        internal GenerationPrep(Vector2Int contentSize, Vector2Int targetSize)
        {
            this.contentSize = contentSize;
            this.targetSize  = targetSize;
        }
    }

    static class ShaderId
    {
        public static readonly int CONTOUR           = Shader.PropertyToID("_Contour");
        public static readonly int CORNER            = Shader.PropertyToID("_Corner");
        public static readonly int ALPHA_TEX         = Shader.PropertyToID("_AlphaTex");
        public static readonly int ALPHA_THRESHOLD   = Shader.PropertyToID("_AlphaThreshold");
        public static readonly int STEP_WIDTH        = Shader.PropertyToID("_StepWidth");
        public static readonly int SMOOTH_FACTOR     = Shader.PropertyToID("_SmoothFactor");
        public static readonly int TARGET_SIZE       = Shader.PropertyToID("_TargetSize");
        public static readonly int TARGET_OFFSET     = Shader.PropertyToID("_TargetOffset");
        public static readonly int BEND_ANGLE_CUTOFF = Shader.PropertyToID("_BendAngleCutoff");
        public static readonly int SIGMA_NEAR        = Shader.PropertyToID("_SigmaNear");
        public static readonly int SIGMA_GROWTH      = Shader.PropertyToID("_SigmaGrowth");
        public static readonly int SIGMA_MAX         = Shader.PropertyToID("_SigmaMax");
        public static readonly int FIT_ERROR_CUTOFF  = Shader.PropertyToID("_FitErrorCutoff");
        public static readonly int CHAIN_MAX_STEPS   = Shader.PropertyToID("_ChainMaxSteps");
        public static readonly int SMOOTHING_RADIUS  = Shader.PropertyToID("_SmoothingRadius");
        public static readonly int DISTANCE_RANGE    = Shader.PropertyToID("_DistanceRange");

        public static int passAlphaPreprocess;
        public static int passContour;
        public static int passContourSmooth;
        public static int passCornerDetect;
        public static int passJumpFloodSeed;
        public static int passJumpFlood;
        public static int passResolve;
        public static int passFilter;
    }

    static Material _mat;

    static Material Mat
    {
        get
        {
            if (!_mat)
            {
                _mat                         = new Material(Shader.Find("Hidden/Paraform/Form-Sprite-Generate"));
#if UNITY_EDITOR
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseMaterial;
#endif
                ShaderId.passAlphaPreprocess = _mat.FindPass("alphaPreprocess");
                ShaderId.passContour         = _mat.FindPass("Contour");
                ShaderId.passContourSmooth   = _mat.FindPass("ContourSmooth");
                ShaderId.passCornerDetect    = _mat.FindPass("CornerDetect");
                ShaderId.passJumpFloodSeed   = _mat.FindPass("JumpFloodSeed");
                ShaderId.passJumpFlood       = _mat.FindPass("JumpFlood");
                ShaderId.passResolve         = _mat.FindPass("Resolve");
                ShaderId.passFilter          = _mat.FindPass("Filter");
            }

            return _mat;
        }
    }

#if UNITY_EDITOR
    static void ReleaseMaterial()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseMaterial;
        if (_mat)
            UnityEngine.Object.DestroyImmediate(_mat);
        _mat = null;
    }
#endif

    public static GenerationPrep Prepare(in FormAssetGenerationParams parameters)
    {
        var source = parameters.source;
        var contentSize = source.width >= source.height
            ? new Vector2Int(parameters.resolution,                                                         Mathf.RoundToInt(parameters.resolution * (float)source.height / source.width))
            : new Vector2Int(Mathf.RoundToInt(parameters.resolution * (float)source.width / source.height), parameters.resolution);
        var targetSize = contentSize + Vector2Int.one * (parameters.padding * 2);
        return new GenerationPrep(contentSize, targetSize);
    }

    public static void Generate(
        in FormAssetGenerationParams parameters,
        in GenerationPrep            prep,
        RenderTexture                destination
    )
    {
        using var debugScope = FormAssetDebug.BeginGeneration();

        var           source            = parameters.source;
        var           prevActive        = RenderTexture.active;
        RenderTexture preprocessedAlpha = null;
        RenderTexture contour           = null;
        RenderTexture corner            = null;
        RenderTexture jumpFloodA        = null;
        RenderTexture jumpFloodB        = null;
        RenderTexture generatedTex      = null;
        try
        {
            var width       = source.width;
            var height      = source.height;
            var targetW     = prep.targetSize.x;
            var targetH     = prep.targetSize.y;
            var contentSize = (Vector2)prep.contentSize;
            var sourceSize  = new Vector2(width, height);
            var offset      = Vector2.one * parameters.padding;
            FormAssetDebug.UpdateSelectedCoordinates(sourceSize, contentSize, offset);
            var half                 = new Vector2(.5f, .5f);
            var sourceToTargetCoord  = new Remap(Vector2.zero, sourceSize,        offset, offset + contentSize);
            var contourToTargetCoord = new Remap(half,         sourceSize + half, offset, offset + contentSize);
            FormAssetDebug.Capture("sourceAlpha", source,
                                   coordRemap: sourceToTargetCoord,
                                   viewMode: FormAssetDebug.ViewMode.Alpha);
            var maxDist = Mathf.Ceil(Mathf.Max(targetW, targetH) / 2f);

            var cellSize         = contentSize.x / width;
            var sigmaNearWorld   = parameters.contourSmooth / 3f * cellSize;
            var sigmaGrowthWorld = 0; //parameters.sigmaGrowth * Mathf.Sqrt(cellSize);
            var sigmaMax         = sigmaNearWorld + sigmaGrowthWorld * Mathf.Sqrt(maxDist);

            var contourW = width + 1;
            var contourH = height + 1;
            Mat.SetFloat(ShaderId.ALPHA_THRESHOLD, Mathf.Lerp(.5f, .4f, parameters.contourShift));
            preprocessedAlpha            = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            preprocessedAlpha.filterMode = FilterMode.Point;
            Graphics.Blit(source, preprocessedAlpha, Mat, ShaderId.passAlphaPreprocess);
            FormAssetDebug.Capture(nameof(ShaderId.passAlphaPreprocess), preprocessedAlpha,
                                   coordRemap: sourceToTargetCoord,
                                   viewMode: FormAssetDebug.ViewMode.Red);

            contour            = RenderTexture.GetTemporary(contourW, contourH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            contour.filterMode = FilterMode.Point;
            Graphics.Blit(preprocessedAlpha, contour, Mat, ShaderId.passContour);
            RenderTexture.ReleaseTemporary(preprocessedAlpha);
            preprocessedAlpha = null;

            FormAssetDebug.Capture(nameof(ShaderId.passContour), contour,
                                   coordRemap: contourToTargetCoord);

            // for (var s = 0; s < parameters.nContourSmoothIter * 2; s++)
            // {
            //     Mat.SetFloat(ShaderId.SMOOTH_FACTOR, (s & 1) == 0 ? parameters.nContourSmoothAmplitude : -(parameters.nContourSmoothAmplitude * (1 + parameters.nContourSmoothRecover)));
            //     var contourSmoothed = RenderTexture.GetTemporary(contourW, contourH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            //     contourSmoothed.filterMode = FilterMode.Point;
            //     Graphics.Blit(contour, contourSmoothed, Mat, ShaderId.passContourSmooth);
            //     RenderTexture.ReleaseTemporary(contour);
            //     contour = contourSmoothed;
            // }

            // FormAssetDebug.Capture(nameof(ShaderId.passContourSmooth), contour,
            //                                coordRemap: contourToTargetCoord);

            Mat.SetTexture(ShaderId.ALPHA_TEX, source);

            Mat.SetVector(ShaderId.TARGET_SIZE,   new Vector4(contentSize.x, contentSize.y, 0, 0));
            Mat.SetVector(ShaderId.TARGET_OFFSET, new Vector4(offset.x,      offset.y,      0, 0));
            Mat.SetFloat(ShaderId.DISTANCE_RANGE,    parameters.distanceRange);
            Mat.SetFloat(ShaderId.BEND_ANGLE_CUTOFF, parameters.bendAngleCutoff * Mathf.Deg2Rad);

            Mat.SetTexture(ShaderId.CONTOUR, contour);
            corner            = RenderTexture.GetTemporary(contourW, contourH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            corner.filterMode = FilterMode.Point;
            FormAssetDebug.UseDrawLayer(nameof(ShaderId.passCornerDetect), priority: 1);
            Graphics.Blit(contour, corner, Mat, ShaderId.passCornerDetect);
            Mat.SetTexture(ShaderId.CORNER, corner);
            var signedCurvatureRange = Mathf.PI / 1.41f * .05f;
            var cornerCaptureRemap = new Remap(new Vector4(0, 0, -signedCurvatureRange, 0),
                                               new Vector4(1, 1, signedCurvatureRange,  1),
                                               Vector4.zero,
                                               Vector4.one);
            FormAssetDebug.Capture(nameof(ShaderId.passCornerDetect), corner,
                                   valRemap: cornerCaptureRemap,
                                   coordRemap: contourToTargetCoord);

            jumpFloodA = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            jumpFloodB = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);

            var iter = Mathf.CeilToInt(Mathf.Log(maxDist, 2f));

            var jfSeed                = iter % 2 == 0 ? jumpFloodB : jumpFloodA;
            var jumpFloodCoordToColor = new Remap(new Vector4(targetW, targetH, 1, 1), new Vector4(1, 1, 0, 0));
            Graphics.Blit(source, jfSeed, Mat, ShaderId.passJumpFloodSeed);
            FormAssetDebug.Capture(nameof(ShaderId.passJumpFloodSeed), jfSeed, valRemap: jumpFloodCoordToColor);
            for (var i = iter; i >= 0; i--)
            {
                Mat.SetFloat(ShaderId.STEP_WIDTH, Mathf.Pow(2, i));
                if (i % 2 == 1)
                    Graphics.Blit(jumpFloodA, jumpFloodB, Mat, ShaderId.passJumpFlood);
                else
                    Graphics.Blit(jumpFloodB, jumpFloodA, Mat, ShaderId.passJumpFlood);
            }
            FormAssetDebug.Capture(nameof(ShaderId.passJumpFlood), jumpFloodA, valRemap: jumpFloodCoordToColor);

            var chainMaxSteps = Mathf.Min(Mathf.CeilToInt(3 * sigmaMax / (0.7f * cellSize)), 256);
            Mat.SetFloat(ShaderId.SIGMA_NEAR,       sigmaNearWorld);
            Mat.SetFloat(ShaderId.SIGMA_GROWTH,     sigmaGrowthWorld);
            Mat.SetFloat(ShaderId.SIGMA_MAX,        sigmaMax);
            Mat.SetFloat(ShaderId.FIT_ERROR_CUTOFF, parameters.fitErrorCutoff);
            Mat.SetInt(ShaderId.CHAIN_MAX_STEPS, chainMaxSteps);
            generatedTex = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            FormAssetDebug.UseDrawLayer(nameof(ShaderId.passResolve));
            Graphics.Blit(jumpFloodA, generatedTex, Mat, ShaderId.passResolve);
            FormAssetDebug.Capture(nameof(ShaderId.passResolve), generatedTex);

            Mat.SetFloat(ShaderId.SMOOTHING_RADIUS, parameters.interiorSmooth);
            FormAssetDebug.UseDrawLayer(nameof(ShaderId.passFilter));
            Graphics.Blit(generatedTex, destination, Mat, ShaderId.passFilter);
            FormAssetDebug.Capture(nameof(ShaderId.passFilter), destination);
        }
        finally
        {
            RenderTexture.active = prevActive;

            if (preprocessedAlpha)
                RenderTexture.ReleaseTemporary(preprocessedAlpha);
            if (contour)
                RenderTexture.ReleaseTemporary(contour);
            if (corner)
                RenderTexture.ReleaseTemporary(corner);
            if (jumpFloodA)
                RenderTexture.ReleaseTemporary(jumpFloodA);
            if (jumpFloodB)
                RenderTexture.ReleaseTemporary(jumpFloodB);
            if (generatedTex)
                RenderTexture.ReleaseTemporary(generatedTex);
        }
    }
}
}
