using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Drawing;

namespace TwoCaptcha {

	public class UserZeroBalanceException : Exception { }
	public class NoSlotAvailableException : Exception { }
	public class AttemptsLimitException : Exception { }

	public class TwoCaptchaClient {

		private static readonly HttpClient client = new HttpClient();
		public string Key { get; private set; }
		public int AttempsLimit { get; set; } = 60;
		public MultipartFormDataContent DefaultNormalCaptchaForm { get; set; }

		public TwoCaptchaClient(string Key) => this.Key = Key;


		public string SolveCaptcha(Image img) {
			using (var ms = new MemoryStream()) {
				img.Save(ms, img.RawFormat);
				var array = ms.ToArray();
				
				var form = new MultipartFormDataContent();
				form.Add(new ByteArrayContent(array, 0, array.Length), "file", "captcha.jpg");
				form.Add(new StringContent("post"), "method");
				return solveCaptcha(form);
			}
		}

		public string SolveCaptcha(string imageBase64) {
			var form = new MultipartFormDataContent();
			form.Add(new StringContent(imageBase64), "body");
			form.Add(new StringContent("base64"), "method");
			return solveCaptcha(form);
		}

		public string SolveReCaptchaV2(string gkey, string url) {

			var postData = new Dictionary<string, string>() {
				{ "key", Key },
				{ "method", "userrecaptcha" },
				{ "googlekey", gkey },
				{ "pageurl", url }
			};
			var response = client.PostAsync("http://2captcha.com/in.php", new FormUrlEncodedContent(postData)).Result.EnsureSuccessStatusCode();
			var responseString = response.Content.ReadAsStringAsync().Result;

			if (!responseString.StartsWith("OK|"))
				switch (responseString) {
					case "ERROR_ZERO_BALANCE":
						throw new UserZeroBalanceException();
					case "ERROR_NO_SLOT_AVAILABLE":
						throw new NoSlotAvailableException();
					default:
						throw new Exception(responseString);
				}
			var id = responseString.Split('|')[1];

			return waitForResult(id);

		}


		private string solveCaptcha(MultipartFormDataContent form) {

			form.Add(new StringContent(Key), "key");
			foreach (var e in DefaultNormalCaptchaForm)
				form.Add(e);
			var response = client.PostAsync("http://2captcha.com/in.php", form).Result.EnsureSuccessStatusCode();
			var responseString = response.Content.ReadAsStringAsync().Result;

			if (!responseString.StartsWith("OK|"))
				switch (responseString) {
					case "ERROR_ZERO_BALANCE":
						throw new UserZeroBalanceException();
					case "ERROR_NO_SLOT_AVAILABLE":
						throw new NoSlotAvailableException();
					default:
						throw new Exception(responseString);
				}
			var id = responseString.Split('|')[1];

			return waitForResult(id);

		}

		private string waitForResult(string id) {

			string path = $"http://2captcha.com/res.php?key={Key}&action=get&id={id}";
			var responseString = "";

			for (int i = 0; i < AttempsLimit; i++) {
				Thread.Sleep(7000);
				responseString = client.GetStringAsync(path).Result;
				if (responseString.StartsWith("OK|"))
					return responseString.Split('|')[1];
				else if (responseString != "CAPCHA_NOT_READY")
					throw new Exception(responseString);
			}

			throw new AttemptsLimitException();

		}

	}

}
