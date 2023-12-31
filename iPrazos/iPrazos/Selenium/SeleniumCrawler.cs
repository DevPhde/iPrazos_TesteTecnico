﻿using Crawler.Events;
using iPrazos.Events;
using iPrazos.Exceptions;
using IPrazos.Entity;
using MediatR;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace iPrazos.Selenium
{
	public class SeleniumCrawler
	{
		private List<ProxyConnection> ProxyList = new List<ProxyConnection>();

		private string HtmlFolderName = "HTML";
		private string FilePathJSON = "proxyList.json";
		// Chrome Config
		private ChromeOptions ChromeOptions;

		private Dictionary<string, List<ProxyConnection>> PageData = new Dictionary<string, List<ProxyConnection>>();

		private bool NextPageExists = true;

		private readonly IMediator _mediator;

		private int ActualPage = 0;

		private bool ProcessPage = true;
		public SeleniumCrawler(IMediator mediator)
		{
			ChromeOptions = new ChromeOptions();
			ChromeOptions.AddArguments("--ignore-certificate-errors");
			_mediator = mediator;
		}

		public void Init(int startPage)
		{
			ActualPage = startPage;

			// HTML FOLDER
			if (!Directory.Exists(HtmlFolderName))
			{
				Directory.CreateDirectory(HtmlFolderName);
			}

			//JSON
			if (!File.Exists(FilePathJSON))
			{
				lock (Program.LockJson)
				{
					File.WriteAllText(FilePathJSON, "{}");
				}
			}

			Console.WriteLine($"{Thread.CurrentThread.Name} Initialized.");


			ChromeOptions.AddArgument("--headless"); // COMENTAR PARA ATIVAR ABERTURA DO NAVEGADOR
			var driver = new ChromeDriver(ChromeOptions);

			string url = $"https://proxyservers.pro/proxy/list/order/updated/order_dir/asc/page/{startPage}";
			try
			{
				driver.Navigate().GoToUrl(url);
				while (NextPageExists)
				{
					ScrapeData(driver);
					SaveHtml(driver);
					SaveJson();
					CrawlerPagination(driver);

					ProxyList.Clear();
				}
				_mediator.Publish(new CrawlerSaveEvent(FilePathJSON));
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}
			driver.Quit();
		}

		private void SaveHtml(ChromeDriver driver)
		{
			string fileName = $"page_{ActualPage}.html";
			string filePath = Path.Combine(HtmlFolderName, fileName);
			if (ProcessPage)
			{
				Program.PagesCrawled++;
				if (!File.Exists(filePath))
				{
					string htmlContent = driver.PageSource;
					File.WriteAllText(filePath, htmlContent);
				}
			}
			Console.WriteLine("Running...");
		}

		private void SaveJson()
		{
			if (ProcessPage)
			{
				_mediator.Publish(new CrawlerJsonUpdateEvent(FilePathJSON, PageData, ActualPage));
			}
		}

		private void ScrapeData(ChromeDriver driver)
		{
			var proxyTable = driver.FindElement(By.ClassName("table-hover"));
			var tableRows = proxyTable.FindElements(By.TagName("tr"));

			if (tableRows.Count > 1)
			{
				foreach (var row in tableRows.Skip(1))
				{
					var columns = row.FindElements(By.TagName("td")).ToList();

					string ipAdress = columns[1].Text;
					int port = int.Parse(columns[2].Text);
					string country = columns[3].Text;
					string protocol = columns[6].Text;

					ProxyConnection proxyConnection = new(ipAdress, port, country, protocol);
					ProxyList.Add(proxyConnection);

					Program.LinesCrawled++;
				}
				PageData[$"Page {ActualPage}"] = ProxyList;
			}
			else
			{
				ProcessPage = false;
				NextPageExists = false;
				return;
			}
		}

		private void CrawlerPagination(ChromeDriver driver)
		{
			try
			{
				var nextPageNumber = driver.FindElements(By.XPath($"//a[number(text()) >= {ActualPage + 1}]"));

				var pageNumberArray = nextPageNumber
				.Select(element => int.TryParse(element.Text, out int pageNumber) ? pageNumber : throw new PaginationException("Internal Error. Sorry for the inconvenience"))
				.Where(pageNumber => pageNumber != -1)
				.ToArray();

				if (pageNumberArray.Length > 0)
				{
					driver.Navigate().GoToUrl($"https://proxyservers.pro/proxy/list/order/updated/order_dir/asc/page/{ActualPage += 2}");
				}
				else
				{
					throw new PaginationException($"{Thread.CurrentThread.Name} Finished.");
				}
			}
			catch (PaginationException ex)
			{
				Console.WriteLine(ex.Message);

				NextPageExists = false;
			}
		}
	}
}
