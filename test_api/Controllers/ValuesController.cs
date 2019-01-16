using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using MediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;

namespace test_api.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class ValuesController : ControllerBase
	{
		private static readonly FormOptions _defaultFormOptions = new FormOptions();
		// POST api/values
		[HttpPost("UploadFiles")]
		[DisableFormValueModelBinding]
		public async Task<IActionResult> Post(string IMEI, string section, string guid, string photoguid, string ukey)
		{
			////long size = files.Sum(f => f.Length);

			////// full path to file in temp location
			////var filePath = Path.GetTempFileName();

			////foreach (var formFile in files)
			////{
			////	if (formFile.Length > 0)
			////	{
			////		using (var stream = new FileStream(filePath, FileMode.Create))
			////		{
			////			await formFile.CopyToAsync(stream);
			////		}
			////	}
			////}

			////return Ok(new { count = files.Count, size, filePath });
			///

			//	var temp = new System.IO.StreamReader(Request.Body, Encoding.ASCII).ReadToEnd();
			try
			{


				if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
				{
					string request_contentType = Request.ContentType;
					return BadRequest($"Expected a multipart request, but got {request_contentType}");
				}
				var formAccumulator = new KeyValueAccumulator();
				string targetFilePath = null;

				var boundary = MultipartRequestHelper.GetBoundary(
					MediaTypeHeaderValue.Parse(Request.ContentType),
					_defaultFormOptions.MultipartBoundaryLengthLimit);
				var reader = new MultipartReader(boundary, HttpContext.Request.Body);

				var section1 = await reader.ReadNextSectionAsync();
				while (section1 != null)
				{
					Microsoft.Net.Http.Headers.ContentDispositionHeaderValue contentDisposition;
					var hasContentDispositionHeader = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section1.ContentDisposition, out contentDisposition);

					if (hasContentDispositionHeader)
					{
						if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
						{
							Random r = new Random();
							int n = r.Next();
							//targetFilePath = Path.GetTempFileName();
							targetFilePath = "D:\\TempFiles2\\" + n.ToString() + "_" + contentDisposition.FileName.Value;
							using (var targetStream = System.IO.File.Create(targetFilePath))
							{
								await section1.Body.CopyToAsync(targetStream);

								//_logger.LogInformation($"Copied the uploaded file '{targetFilePath}'");
							}
						}
						else
						if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
						{
							// Content-Disposition: form-data; name="key"
							//
							// value

							// Do not limit the key name length here because the 
							// multipart headers length limit is already in effect.
							var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
							var encoding = GetEncoding(section1);
							using (var streamReader = new StreamReader(
								section1.Body,
								encoding,
								detectEncodingFromByteOrderMarks: true,
								bufferSize: 1024,
								leaveOpen: true))
							{
								// The value length limit is enforced by MultipartBodyLengthLimit
								var value = await streamReader.ReadToEndAsync();
								if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
								{
									value = String.Empty;
								}
								formAccumulator.Append(key.ToString(), value);

								if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
								{
									throw new InvalidDataException($"Form key count limit {_defaultFormOptions.ValueCountLimit} exceeded.");
								}
							}
						}
					}
					section1 = await reader.ReadNextSectionAsync();

				}
				// Drains any remaining section body that has not been consumed and
				// reads the headers for the next section.
				var formValueProvider = new FormValueProvider(
			BindingSource.Form,
			new FormCollection(formAccumulator.GetResults()),
			CultureInfo.CurrentCulture);
			}
			catch (Exception e)
			{

				throw;
			}
			return Ok();

		}
		private static Encoding GetEncoding(MultipartSection section)
		{
			MediaTypeHeaderValue mediaType;
			var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out mediaType);
			// UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
			// most cases.
			if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
			{
				return Encoding.UTF8;
			}
			return mediaType.Encoding;
		}

		public static class MultipartRequestHelper
		{
			// Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
			// The spec says 70 characters is a reasonable limit.
			public static string GetBoundary(Microsoft.Net.Http.Headers.MediaTypeHeaderValue contentType, int lengthLimit = 70)
			{
				var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
				if (string.IsNullOrWhiteSpace(boundary.Value))
				{
					throw new InvalidDataException("Missing content-type boundary.");
				}

				if (boundary.Length > lengthLimit)
				{
					throw new InvalidDataException(
						$"Multipart boundary length limit {lengthLimit} exceeded.");
				}

				return boundary.Value;
			}

			public static bool IsMultipartContentType(string contentType)
			{
				return !string.IsNullOrEmpty(contentType)
					   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
			}

			public static bool HasFormDataContentDisposition(Microsoft.Net.Http.Headers.ContentDispositionHeaderValue contentDisposition)
			{
				// Content-Disposition: form-data; name="key";
				return contentDisposition != null
					   && contentDisposition.DispositionType.Equals("form-data")
					   && string.IsNullOrEmpty(contentDisposition.FileName.Value)
					   && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
			}

			public static bool HasFileContentDisposition(Microsoft.Net.Http.Headers.ContentDispositionHeaderValue contentDisposition)
			{
				// Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
				return contentDisposition != null
					   && contentDisposition.DispositionType.Equals("form-data")
					   && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
						   || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
			}
		}

		//public static class FileStreamingHelper
		//{
		//	private static readonly FormOptions _defaultFormOptions = new FormOptions();

		//	public static async Task<FormValueProvider> StreamFile(this HttpRequest request, Stream targetStream)
		//	{
		//		if (!MultipartRequestHelper.IsMultipartContentType(request.ContentType))
		//		{
		//			throw new Exception($"Expected a multipart request, but got {request.ContentType}");
		//		}

		//		// Used to accumulate all the form url encoded key value pairs in the 
		//		// request.
		//		var formAccumulator = new KeyValueAccumulator();
		//		string targetFilePath = null;

		//		var boundary = MultipartRequestHelper.GetBoundary(
		//			MediaTypeHeaderValue.Parse(request.ContentType),
		//			_defaultFormOptions.MultipartBoundaryLengthLimit);
		//		var reader = new MultipartReader(boundary, request.Body);

		//		var section = await reader.ReadNextSectionAsync();
		//		while (section != null)
		//		{
		//			var hasContentDispositionHeader = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

		//			if (hasContentDispositionHeader)
		//			{
		//				if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
		//				{
		//					await section.Body.CopyToAsync(targetStream);
		//				}
		//				else if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
		//				{
		//					// Content-Disposition: form-data; name="key"
		//					//
		//					// value

		//					// Do not limit the key name length here because the 
		//					// multipart headers length limit is already in effect.
		//					var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
		//					var encoding = GetEncoding(section);
		//					using (var streamReader = new StreamReader(
		//						section.Body,
		//						encoding,
		//						detectEncodingFromByteOrderMarks: true,
		//						bufferSize: 1024,
		//						leaveOpen: true))
		//					{
		//						// The value length limit is enforced by MultipartBodyLengthLimit
		//						var value = await streamReader.ReadToEndAsync();
		//						if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
		//						{
		//							value = String.Empty;
		//						}
		//						formAccumulator.Append(key.Value, value);

		//						if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
		//						{
		//							throw new InvalidDataException($"Form key count limit {_defaultFormOptions.ValueCountLimit} exceeded.");
		//						}
		//					}
		//				}
		//			}

		//			// Drains any remaining section body that has not been consumed and
		//			// reads the headers for the next section.
		//			section = await reader.ReadNextSectionAsync();
		//		}

		//		// Bind form data to a model
		//		var formValueProvider = new FormValueProvider(
		//			BindingSource.Form,
		//			new FormCollection(formAccumulator.GetResults()),
		//			CultureInfo.CurrentCulture);

		//		return formValueProvider;
		//	}


		//}
	}
}
