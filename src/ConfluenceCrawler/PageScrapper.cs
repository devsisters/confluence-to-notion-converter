using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfluenceCrawler;

public sealed class PageScrapper
{
    private readonly ILogger _logger;

    public PageScrapper(ILogger<PageScrapper> logger)
    {
        _logger = logger;
    }
}
