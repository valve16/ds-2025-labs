using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace System.E2ETests
{
    public class ValuatorE2ETests : IDisposable
    {
        private readonly ChromeDriver driver;

        public ValuatorE2ETests()
        {
            // Инициализация WebDriver (Chrome)
            driver = new ChromeDriver();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        }

        [Theory]
        [InlineData("a1", 0.5)]
        [InlineData("Hello", 0)]
        [InlineData("ABCD1234", 0.5)]
        [InlineData("ABСD12", 2.0 / 6)]
        public void TestSubmitTextAndCheckRank(string inputText, double expectedRank)
        {
            driver.Navigate().GoToUrl("http://localhost:5001/");

            var textInput = driver.FindElement(By.Name("text"));
            textInput.SendKeys(inputText);

            var countrySelect = driver.FindElement(By.Name("country"));
            var selectElement = new SelectElement(countrySelect);
            selectElement.SelectByValue("Russia");

            var submitButton = driver.FindElement(By.CssSelector("input[type=submit]"));
            submitButton.Click();

            //Thread.Sleep(2000); // Подождать 2 секунды перед проверкой
            //driver.Navigate().Refresh();
            //for (int i = 0; i < 5; i++)
            //{
            //    if (driver.FindElement(By.TagName("p")).Text == "Оценка содержания не завершена")
            //    {
            //        driver.Navigate().Refresh();
            //        Thread.Sleep(500);
            //    }
            //    else
            //    {
            //        continue;
            //    }    
            //}
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(5));
            wait.Until(d => d.FindElement(By.TagName("p")).Text != "Оценка содержания не завершена");


            //Получение значения rank из текста 
            var rankElement = driver.FindElement(By.TagName("p"));
            string rankText = rankElement.Text.Replace("Оценка содержания: ", "").Trim();
            rankText = rankText.Replace(",", ".");
            double rank = double.Parse(rankText, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(expectedRank, rank); 
        }

        public void Dispose()
        {
            driver.Quit();
        }
    }
}