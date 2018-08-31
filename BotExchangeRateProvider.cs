using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml;
using HtmlAgilityPack;
using Nop.Core;
using Nop.Core.Plugins;
using Nop.Plugin.ExchangeRate.BotExchange;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.ExchangeRate.EcbExchange
{
    public class BotExchangeRateProvider : BasePlugin, IExchangeRateProvider
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public BotExchangeRateProvider(ILocalizationService localizationService,
            ILogger logger)
        {
            this._localizationService = localizationService;
            this._logger = logger;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets currency live rates
        /// Rate Url is : https://rate.bot.com.tw/xrt?Lang=zh-TW
        /// </summary>
        /// <param name="exchangeRateCurrencyCode">Exchange rate currency code</param>
        /// <returns>Exchange rates</returns>
        public IList<Core.Domain.Directory.ExchangeRate> GetCurrencyLiveRates(string exchangeRateCurrencyCode)
        {
            if (exchangeRateCurrencyCode == null)
                throw new ArgumentNullException(nameof(exchangeRateCurrencyCode));

            //add twd with rate 1
            var ratesToTwd = new List<Core.Domain.Directory.ExchangeRate>
            {
                new Core.Domain.Directory.ExchangeRate
                {
                    CurrencyCode = "TWD",
                    Rate = 1,
                    UpdatedOn = DateTime.UtcNow
                }
            };

            //collect rate
            var currencyList = new HtmlWeb().Load("https://rate.bot.com.tw/xrt?Lang=zh-TW").DocumentNode.SelectNodes("//tbody/tr");
            var rateResult = new List<BotRateObject>();
            foreach(var rate in currencyList)
            { 
                string currency = rate.SelectSingleNode("td[@data-table='幣別']/div/*[3]").InnerText.Trim();
                string[] splitCurrencyString = null;
                splitCurrencyString = currency.Split("(");
                currency = splitCurrencyString[0];
                string currencyCode = splitCurrencyString[1].Replace(")", String.Empty);
                string cashBuying = rate.SelectSingleNode("td[@data-table='本行現金買入']").InnerText;
                string cashSelling= rate.SelectSingleNode("td[@data-table='本行現金賣出']").InnerText;
                string spotBuying = rate.SelectSingleNode("td[@data-table='本行即期買入']").InnerText;
                string spotSelling = rate.SelectSingleNode("td[@data-table='本行即期賣出']").InnerText;
                //add to result list
                rateResult.Add(new BotRateObject
                {
                    Currency = currency,
                    CurrencyCode = currencyCode,
                    CashBuying = (!cashBuying.Contains("-")) ? Convert.ToDecimal(cashBuying) : new decimal?(),
                    CashSelling = (!cashSelling.Contains("-")) ? Convert.ToDecimal(cashSelling) : new decimal?(),
                    SpotBuying = (!spotBuying.Contains("-")) ? Convert.ToDecimal(spotBuying) : new decimal?(),
                    SpotSelling = (!spotSelling.Contains("-")) ? Convert.ToDecimal(spotSelling) : new decimal?(),
                });
            }

            //converr rate to nop currency rate object
            foreach(var rate in rateResult)
            { 
                var averageRate = rate.SpotBuying!=null
                    ? (1 / (( rate.SpotBuying.Value + rate.SpotSelling.Value ) / 2))
                    : (1 / (( rate.CashBuying.Value + rate.CashBuying.Value ) / 2));

                ratesToTwd.Add(new Core.Domain.Directory.ExchangeRate
                { 
                    CurrencyCode = rate.CurrencyCode,
                    Rate = averageRate,
                    UpdatedOn = DateTime.UtcNow,
                });
            }
            
            //return result for the euro
            if (exchangeRateCurrencyCode.Equals("twd", StringComparison.InvariantCultureIgnoreCase))
                return ratesToTwd;

            //use only currencies that are supported by BOT
            var exchangeRateCurrency = ratesToTwd.FirstOrDefault(rate => rate.CurrencyCode.Equals(exchangeRateCurrencyCode, StringComparison.InvariantCultureIgnoreCase));
            if (exchangeRateCurrency == null)
                throw new NopException(_localizationService.GetResource("Plugins.ExchangeRate.BotExchange.Error"));

            //return result for the selected (not euro) currency
            return ratesToTwd.Select(rate => new Core.Domain.Directory.ExchangeRate
            {
                CurrencyCode = rate.CurrencyCode,
                Rate = Math.Round(rate.Rate / exchangeRateCurrency.Rate, 4),
                UpdatedOn = rate.UpdatedOn
            }).ToList();
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.ExchangeRate.BotExchange.Error", "You can use BOT (Back of taiwan) exchange rate provider only when the primary exchange rate currency is supported by BOT");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.ExchangeRate.BotExchange.Error");

            base.Uninstall();
        }

        #endregion

    }
}