using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DivingPrototype
{
    public static class OceanSilhouetteBaker
    {
        const string Art = "Assets/Prototype/Interactions/Art/";
        [MenuItem("Diving Prototype/Rebuild Smooth Ocean Silhouettes")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Bake outside Play mode.");
            var ocean = UnityEngine.Object.FindAnyObjectByType<OceanInteractions>();
            if (ocean == null) throw new InvalidOperationException("Open Ocean Interactables first.");
            var polygons = ocean.oceanMap.GetComponentsInChildren<PolygonCollider2D>();
            if (polygons.Any(p => p.GetComponent<SpriteRenderer>().drawMode != SpriteDrawMode.Simple))
                throw new InvalidOperationException("Undo map resizing before rebuilding shared ocean silhouettes. Resized terrain is kept in scene properties only.");
            var first = polygons[0].GetComponent<SpriteRenderer>().sprite;
            float factor = 40f / first.pixelsPerUnit;
            int width = Mathf.RoundToInt(first.texture.width * factor);
            int height = Mathf.RoundToInt(first.texture.height * factor);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255,255,255,0);
            var sprites = new List<(Sprite sprite, Rect rect)>();
            foreach (var polygon in polygons)
            {
                var sprite = polygon.GetComponent<SpriteRenderer>().sprite;
                var rect = new Rect(sprite.rect.position * factor, sprite.rect.size * factor);
                var points = polygon.points.Select(p => (p + polygon.offset) * 40f + rect.position).ToArray();
                var crossings = new List<float>();
                var row = new float[Mathf.CeilToInt(rect.width)];
                // Integrate four subpixel scanlines rather than enlarging existing jagged alpha.
                for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int sample = 0; sample < 4; sample++)
                    {
                        float sy = y + (sample + .5f) / 4f;
                        crossings.Clear();
                        for (int i = 0; i < points.Length; i++)
                        {
                            var a = points[i]; var b = points[(i+1)%points.Length];
                            if ((a.y > sy) != (b.y > sy)) crossings.Add(a.x + (sy-a.y)*(b.x-a.x)/(b.y-a.y));
                        }
                        crossings.Sort();
                        for (int i = 0; i+1 < crossings.Count; i+=2)
                            for (int x = Mathf.Max((int)rect.xMin, Mathf.FloorToInt(crossings[i])); x < Mathf.Min((int)rect.xMax, Mathf.CeilToInt(crossings[i+1])); x++)
                                row[x-(int)rect.xMin] += Mathf.Max(0,Mathf.Min(x+1,crossings[i+1])-Mathf.Max(x,crossings[i]))*.25f;
                    }
                    for (int x = 0; x < row.Length; x++) pixels[y*width+(int)rect.xMin+x].a = (byte)Mathf.RoundToInt(row[x]*255);
                }
                sprites.Add((sprite,rect));
            }
            var texture = SaveTexture(Art+"DraftSeafloor.png", width, height, pixels);
            foreach (var entry in sprites) Replace(entry.sprite, texture, entry.rect, Vector2.zero,40);
            const int size = 256;
            pixels = new Color32[size*size*2];
            for(int y=0;y<size;y++) for(int x=0;x<size*2;x++)
            {
                float radius = Vector2.Distance(new Vector2(x%size+.5f,y+.5f),Vector2.one*(size*.5f));
                float alpha = Mathf.Clamp01(size*.5f-radius+.5f);
                if(x>=size) alpha *= Mathf.Clamp01(radius-size*.375f+.5f);
                pixels[y*size*2+x] = new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
            }
            texture=SaveTexture(Art+"SmoothMarkers.png",size*2,size,pixels);
            Replace(AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Circle.asset"),texture,new Rect(0,0,size,size),Vector2.one*.5f,size);
            Replace(AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Ring.asset"),texture,new Rect(size,0,size,size),Vector2.one*.5f,size);
            AssetDatabase.SaveAssets();
            Debug.Log("Smooth silhouettes baked: 26 terrain sprites at 40 PPU; circle/ring at 256 PPU.");
        }
        static Texture2D SaveTexture(string path,int width,int height,Color32[] pixels)
        {
            var generated=new Texture2D(width,height,TextureFormat.RGBA32,false);
            generated.SetPixels32(pixels); generated.Apply();
            System.IO.File.WriteAllBytes(path,generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default; importer.maxTextureSize=Mathf.NextPowerOfTwo(Mathf.Max(width,height));
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            importer.alphaIsTransparency=true; importer.isReadable=false;
            var platform=importer.GetDefaultPlatformTextureSettings();
            platform.format=width>512 ? TextureImporterFormat.BC7 : TextureImporterFormat.RGBA32;
            platform.textureCompression=width>512 ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            platform.compressionQuality=100; importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Replace(Sprite target,Texture2D texture,Rect rect,Vector2 pivot,float ppu)
        {
            var generated=Sprite.Create(texture,rect,pivot,ppu,0,SpriteMeshType.FullRect);
            generated.name=target.name; EditorUtility.CopySerialized(generated,target);
            EditorUtility.SetDirty(target); UnityEngine.Object.DestroyImmediate(generated);
        }
    }
}
