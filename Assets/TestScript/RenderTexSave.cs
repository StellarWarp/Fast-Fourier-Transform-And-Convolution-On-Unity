using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TestScript
{
    [ExecuteAlways]
    public class RenderTexSave : MonoBehaviour
    {
        public bool save = false;
        public bool saveFromCurrent = false;
        public bool saveFromScreen = false;
        public Vector2Int size = new Vector2Int(1600, 900);

        [Serializable]
        public struct TextInfo
        {
            public RenderTexture renderTexture;
            public bool normalize;
        }
        public List<TextInfo> renderTextures = new List<TextInfo>();
        
        private void Update()
        {

            if (save)
            {
                save = false;
                saveFromCurrent = true;
                saveFromScreen = true;
            }
            if (saveFromCurrent)
            {
                saveFromCurrent = false;
                foreach (var info in renderTextures)
                {
                    RenderTexture renderTexture = info.renderTexture;
                    bool normalize = info.normalize;
                    if (renderTexture == null) continue;
                    Texture2D texSave = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                    RenderTexture.active = renderTexture;
                    texSave.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    //进行归一化操作 img / img.sum
                    if (normalize)
                    {
                        Color max = new Color(0, 0, 0, 0);
                        for (int i = 0; i < texSave.width; i++)
                        {
                            for (int j = 0; j < texSave.height; j++)
                            {
                                Color pixel = texSave.GetPixel(i, j);
                                max.r = Mathf.Max(max.r, pixel.r);
                                max.g = Mathf.Max(max.g, pixel.g);
                                max.b = Mathf.Max(max.b, pixel.b);
                                max.a = Mathf.Max(max.a, pixel.a);
                            }
                        }
                        for (int i = 0; i < texSave.width; i++)
                        {
                            for (int j = 0; j < texSave.height; j++)
                            {
                                Color pixel = texSave.GetPixel(i, j);
                                pixel.r /= max.r;
                                pixel.g /= max.g;
                                pixel.b /= max.b;
                                pixel.a /= max.a;
                                texSave.SetPixel(i, j, pixel);
                            }
                        }
                    }
                    for (int i = 0; i < texSave.width; i++)
                    {
                        for (int j = 0; j < texSave.height; j++)
                        {
                            Color pixel = texSave.GetPixel(i, j);
                            pixel.a = 1;
                            texSave.SetPixel(i, j, pixel);
                        }
                    }
                    texSave.Apply();
                    SaveTex(texSave, $"{renderTexture.name}");
                }

            }

            if (saveFromScreen)
            {
                StartCoroutine(CaptureScreenshot());
            }

        }
        

        private void OnGUI()
        {

        }
        
        IEnumerator CaptureScreenshot()
        {
            //只在每一帧渲染完成后才读取屏幕信息
            yield return new WaitForEndOfFrame();
            saveFromScreen = false;

            Texture2D texSave = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            //读取屏幕像素信息并存储为纹理数据
            texSave.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texSave.Apply();
            SaveTex(texSave, "Tone mapping");
        }

        void SaveTex(Texture2D texSave,string name)
        {
            byte[] vs = texSave.EncodeToPNG();
            string path = @"C:\Users\Estelle\Desktop\RebderTex\"+ name + ".png";
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            fileStream.Write(vs, 0, vs.Length);
            fileStream.Dispose();
            fileStream.Close();
        }
    }
}