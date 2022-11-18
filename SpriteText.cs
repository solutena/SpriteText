using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.U2D;
#endif

[AddComponentMenu("UI/SpriteText")]
public class SpriteText : Text
{
	[SerializeField]
	SpriteAtlas spriteAtlas;

	static readonly Regex spriteRegex = new Regex(@"(?i)<(?:sprite=)(.*?)(?: x=(.*?))?(?: y=(.*?))?(?: size=(.*?))?\/>", RegexOptions.Multiline);

	string OutputText { get; set; }
	Action DelayAction { get; set; }

	public override string text
	{
		get => base.text;
		set
		{
			base.text = value;
			Changed();
			DelayAction?.Invoke();
		}
	}

	protected override void OnPopulateMesh(VertexHelper toFill)
	{
		if (spriteAtlas == null)
		{
			base.OnPopulateMesh(toFill);
			return;
		}
		string orignText = m_Text;
		m_Text = OutputText;
		base.OnPopulateMesh(toFill);
		m_Text = orignText;
	}

	private void Update()
	{
		DelayAction?.Invoke();
	}

	public override void SetVerticesDirty()
	{
		base.SetVerticesDirty();
		if (spriteAtlas == null)
			return;
		Changed();
	}

	public void Changed()
	{
		if (gameObject.activeInHierarchy == false)
			return;

		OutputText = m_Text;

		MatchCollection matchCollection = spriteRegex.Matches(m_Text);
		Sprite[] sprites = new Sprite[matchCollection.Count];
		int[] matchIndexs = new int[matchCollection.Count];

		int matchOffset = 0;
		for (int i = 0; i < matchCollection.Count; i++)
		{
			Match match = matchCollection[i];
			Sprite sprite = GetSprite(match.Groups[1].Value, spriteAtlas);
			if (sprite == null)
				continue;
			OutputText = OutputText.Remove(match.Index - matchOffset, match.Length);
			matchIndexs[i] = match.Index - matchOffset;
			sprites[i] = sprite;
			matchOffset += match.Length;
		}

		DelayAction = delegate
		{
			List<Image> imageList = new List<Image>();
			GetComponentsInChildren(imageList);

			for (int i = 0; i < matchCollection.Count; i++)
			{
				if (i < imageList.Count)
					continue;
				GameObject spriteObject = new GameObject("RichSprite");
				spriteObject.transform.SetParent(rectTransform);
				spriteObject.transform.localScale = Vector3.one;
				Image image = spriteObject.AddComponent<Image>();
				image.raycastTarget = false;
				imageList.Add(image);
			}

			TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
			cachedTextGenerator.Populate(OutputText, settings);

			int removeLength = 0;
			for (int i = 0; i < matchCollection.Count; i++)
			{
				Sprite sprite = sprites[i];
				Image image = imageList[i];
				if (sprite == null)
				{
					image.enabled = false;
					continue;
				}
				Match match = matchCollection[i];
				int matchIndex = matchIndexs[i];
				if (cachedTextGenerator.characters.Count <= matchIndex)
				{
					image.enabled = false;
					continue;
				}
				UICharInfo uiCharInfo = cachedTextGenerator.characters[matchIndex];
				UILineInfo uiLineInfo = cachedTextGenerator.lines[0];
				Vector2 position = uiCharInfo.cursorPos;
				float.TryParse(match.Groups[2].Value, out float x);
				float.TryParse(match.Groups[3].Value, out float y);
				float size = 1;
				if (match.Groups[4].Success)
					float.TryParse(match.Groups[4].Value, out size);
				position.x += x;
				position.y += y;
				position.y -= uiLineInfo.height * 0.5f;
				position *= (1 / pixelsPerUnit);
				removeLength += match.Length;
				//sprite
				image.enabled = true;
				image.sprite = sprite;
				image.SetNativeSize();
				image.rectTransform.localScale = Vector3.one * size;
				image.rectTransform.localPosition = position;
			}

			for (int i = transform.childCount - 1; i >= matchCollection.Count; i--)
				DestroyImmediate(transform.GetChild(i).gameObject);
			DelayAction = null;
		};
	}

#if UNITY_EDITOR
	[MenuItem("GameObject/UI/SpriteText")]
	static void CreateSpriteText()
	{
		GameObject go = new GameObject("SpriteText");
		go.AddComponent<SpriteText>();

		if (Selection.activeGameObject == null)
		{
			Canvas canvas = FindObjectOfType<Canvas>();
			if (canvas == null)
			{
				GameObject canvasGo = new GameObject("Canvas");
				canvas = canvasGo.AddComponent<Canvas>();
			}
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			go.transform.SetParent(canvas.transform);
			go.transform.localPosition = Vector3.zero;
		}
		else
			go.transform.SetParent(Selection.activeGameObject.transform);
		Undo.RegisterCreatedObjectUndo(go, "CreateSpriteText");
		Selection.activeGameObject = go;
	}

	protected override void OnEnable()
	{
		if (spriteAtlas == null)
		{
			Debug.LogError("SpriteAtlas is null", gameObject);
			return;
		}
	}

	public Sprite GetSprite(string name, SpriteAtlas spriteAtlas)
	{
		foreach (var packable in spriteAtlas.GetPackables())
		{
			var dirPath = UnityEditor.AssetDatabase.GetAssetPath(packable);
			var spriteGUIDs = UnityEditor.AssetDatabase.FindAssets($"{name} t:sprite", new string[] { dirPath });
			if (spriteGUIDs.Length == 0)
				continue;
			for (int i = 0; i < spriteGUIDs.Length; i++)
			{
				var spritePath = UnityEditor.AssetDatabase.GUIDToAssetPath(spriteGUIDs[i]);
				var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
				if (sprite.name == name)
					return sprite;
			}
		}
		return null;
	}
#else
    public Sprite GetSprite(string name, SpriteAtlas spriteAtlas)
    {
        return spriteAtlas.GetSprite(name);
    }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CanEditMultipleObjects]
[UnityEditor.CustomEditor(typeof(SpriteText))]
public class SpriteTextEditor : UnityEditor.UI.TextEditor
{
	UnityEditor.SerializedProperty spriteAtlas;

	protected override void OnEnable()
	{
		base.OnEnable();
		spriteAtlas = serializedObject.FindProperty("spriteAtlas");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		base.OnInspectorGUI();
		UnityEditor.EditorGUILayout.PropertyField(spriteAtlas);
		serializedObject.ApplyModifiedProperties();
	}
}
#endif
