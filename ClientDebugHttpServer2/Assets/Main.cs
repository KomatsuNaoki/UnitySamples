﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Kayac;

public class Main : MonoBehaviour
{
	[SerializeField]
	RawImage image;
	[SerializeField]
	AudioSource audioSource;
	[SerializeField]
	TextAsset debugServerIndexHtmlAsset;
	[SerializeField]
	int debugServerPort;
	[SerializeField]
	Text logText;

	string debugServerIndexHtml;
	DebugServer debugServer;
	float rotationSpeed;
	Coroutine coroutine;
	bool loadRequested;

	void Start()
	{
		Application.logMessageReceived += OnLogReceived;
		debugServerIndexHtml = debugServerIndexHtmlAsset.text;

		debugServer = new DebugServer(debugServerPort, "/assets/", OnFileChanged);

		// 上書き検出
		debugServer.RegisterRequestCallback("/", OnWebRequestRoot);
		debugServer.RegisterRequestCallback("/api/file/upload", OnWebRequestUploadFile);
		debugServer.RegisterRequestCallback("/api/file/delete", OnWebRequestDeleteFile);
		debugServer.RegisterRequestCallback("/api/file/delete-all", OnWebRequestDeleteAllFile);
		loadRequested = true;
	}

	void OnLogReceived(string message, string callStack, LogType type)
	{
		logText.text += message + '\n';
	}

	void OnWebRequestRoot(out string outputHtml, NameValueCollection queryString, Stream bodyData)
	{
		// html返して終わり
		outputHtml = debugServerIndexHtml;
	}

	void OnWebRequestUploadFile(out string outputHtml, NameValueCollection queryString, Stream bodyData)
	{
		outputHtml = null;
		if (bodyData == null)
		{
			outputHtml = "中身が空.";
			return;
		}
		var path = queryString["path"];
		if (string.IsNullOrEmpty(path))
		{
			outputHtml = "アップロードしたファイルのパスが空.";
			return;
		}
		DebugServerUtil.SaveOverride(path, bodyData);
		loadRequested = true;
	}

	void OnWebRequestDeleteFile(out string outputHtml, NameValueCollection queryString, Stream bodyData)
	{
		outputHtml = null;
		var path = queryString["path"];
		if (string.IsNullOrEmpty(path))
		{
			outputHtml = "アップロードしたファイルのパスが空.";
			return;
		}
		DebugServerUtil.DeleteOverride(path);
		loadRequested = true;
	}

	void OnWebRequestDeleteAllFile(out string outputHtml, NameValueCollection queryString, Stream bodyData)
	{
		DebugServerUtil.DeleteAllOverride();
		outputHtml = null;
		loadRequested = true;
	}

	void Update()
	{
		// 絵を回転
		var angles = image.transform.localRotation.eulerAngles;
		angles.z += rotationSpeed;
		image.transform.localRotation = Quaternion.Euler(angles);

		// 音鳴らす
		if (!audioSource.isPlaying)
		{
			audioSource.Play();
		}
		debugServer.ManualUpdate();

		// ロード。重複実行を防ぐ
		if (loadRequested && (coroutine == null))
		{
			loadRequested = false;
			coroutine = StartCoroutine(CoLoad());
		}
	}


	[System.Serializable]
	class UploadFileArg
	{
		public string path;
		public string contentBase64;
	}

	[System.Serializable]
	class RotationSpeedData
	{
		public float rotationSpeed;
	}

	IEnumerator CoLoad()
	{
		audioSource.Stop();
		var retJson = new CoroutineReturnValue<string>();
		yield return DebugServerUtil.CoLoad(retJson, "Jsons/rotation_speed.json");
		if (retJson.Exception != null)
		{
			Debug.LogException(retJson.Exception);
		}
		var retImage = new CoroutineReturnValue<Texture2D>();
		yield return DebugServerUtil.CoLoad(retImage, "Images/image.png");
		if (retImage.Exception != null)
		{
			Debug.LogException(retImage.Exception);
		}
		var retSound = new CoroutineReturnValue<AudioClip>();
		yield return DebugServerUtil.CoLoad(retSound, "Sounds/sound.wav");
		if (retSound.Exception != null)
		{
			Debug.LogException(retSound.Exception);
		}

		if (retJson.Value != null)
		{
			rotationSpeed = JsonUtility.FromJson<RotationSpeedData>(retJson.Value).rotationSpeed;
		}

		if (retImage.Value != null)
		{
			image.texture = retImage.Value;
		}

		if (retSound.Value != null)
		{
			audioSource.clip = retSound.Value;
		}
		coroutine = null;
	}

	void OnFileChanged(string path)
	{
		loadRequested = true;
	}
}
