using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Reqnroll;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for scenarios tagged <c>@ui</c>: a real Chromium (Playwright) drives the web interface of a running <c>shelf serve</c>.
/// The browser is started once per test run; every scenario gets its own context (cookies, storage, viewport, color scheme).
/// </summary>
[Binding]
public sealed class BrowserSteps(WebSteps web, ConfigSteps workspace) : IDisposable
{
    private static readonly float Timeout = 10_000;

    private static IPlaywright? s_playwright;
    private static IBrowser? s_browser;
    private static readonly Lock Gate = new();

    private IBrowserContext? _context;
    private bool _dark;
    private (int Width, int Height) _viewport = (1280, 900);
    private readonly List<string> _browserErrors = [];
    private readonly List<string> _traffic = [];

    /// <summary>The page of the scenario, once a step has opened one.</summary>
    public IPage Page { get; private set; } = null!;

    private (bool Dark, (int Width, int Height) Viewport) _contextKey;

    [BeforeTestRun]
    public static void InstallBrowser()
    {
        // Idempotent and quick when the browser is there. On a Linux runner the system libraries are installed with it.
        var arguments = OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("CI") == "true"
            ? new[] { "install", "--with-deps", "chromium" }
            : ["install", "chromium"];
        var code = Microsoft.Playwright.Program.Main(arguments);
        if (code != 0)
        {
            throw new InvalidOperationException($"Playwright could not install Chromium (exit code {code}); the scenarios tagged @ui need it.");
        }
    }

    [AfterTestRun]
    public static void CloseBrowser()
    {
        s_browser?.CloseAsync().GetAwaiter().GetResult();
        s_playwright?.Dispose();
    }

    private static IBrowser Browser()
    {
        lock (Gate)
        {
            s_playwright ??= Playwright.CreateAsync().GetAwaiter().GetResult();
            s_browser ??= s_playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
            return s_browser;
        }
    }

    // ---- the browser ----

    [Given("the browser prefers a {word} color scheme")]
    public void GivenTheBrowserPrefers(string scheme) => _dark = scheme == "dark";

    [Given("the browser window is {int} x {int} pixels")]
    public void GivenTheWindowIs(int width, int height) => _viewport = (width, height);

    [When("I open {string} in the browser")]
    [Given("I open {string} in the browser")]
    public void WhenIOpen(string address)
    {
        // The same browser keeps its cookies from one page to the next; a new one starts when the window or the scheme changed.
        if (Page is null || _contextKey != (_dark, _viewport))
        {
            Page = NewPage();
        }

        Page.GotoAsync(new Uri(web.BaseAddress, address).ToString()).GetAwaiter().GetResult();
    }

    [Then("the page shows the text {string}")]
    public void ThenThePageShows(string text) => Assert.True(Wait(Page.GetByText(text).First), $"The page does not show '{text}'. {Dump()}");

    [Then("the page does not show the text {string}")]
    public void ThenThePageDoesNotShow(string text) => Assert.True(Gone(Page.GetByText(text)), $"The page still shows '{text}'. {Dump()}");

    [When("I click the {string} link")]
    [Given("I click the {string} link")]
    public void WhenIClickTheLink(string name) => Page.GetByRole(AriaRole.Link, new() { Name = name }).First.ClickAsync().GetAwaiter().GetResult();

    [When("I click the {string} button")]
    [Given("I click the {string} button")]
    public void WhenIClickTheButton(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name }).First.ClickAsync().GetAwaiter().GetResult();

    [When("I fill the {string} field with {string}")]
    [Given("I fill the {string} field with {string}")]
    public void WhenIFill(string label, string text) =>
        Page.GetByLabel(label, new() { Exact = true }).FillAsync(text.Replace("\\n", "\n", StringComparison.Ordinal)).GetAwaiter().GetResult();

    [When("I press Enter in the {string} field")]
    [Given("I press Enter in the {string} field")]
    public void WhenIPressEnter(string label) => Page.GetByLabel(label, new() { Exact = true }).PressAsync("Enter").GetAwaiter().GetResult();

    [When("I press Escape")]
    [Given("I press Escape")]
    public void WhenIPressEscape() => Page.Keyboard.PressAsync("Escape").GetAwaiter().GetResult();

    [When("I choose {string} in the {string} list")]
    [Given("I choose {string} in the {string} list")]
    public void WhenIChoose(string option, string label) =>
        Page.GetByLabel(label, new() { Exact = true }).SelectOptionAsync(new SelectOptionValue { Label = option }).GetAwaiter().GetResult();

    [When("I reload the page")]
    [Given("I reload the page")]
    public void WhenIReload() => Page.ReloadAsync().GetAwaiter().GetResult();

    [Then("the {string} button is pressed")]
    [Given("the {string} button is pressed")]
    public void ThenPressed(string name) => Expect(Page.GetByRole(AriaRole.Button, new() { Name = name }).First, e => e.ToHaveAttributeAsync("aria-pressed", "true"));

    [Then("the {string} button is not pressed")]
    public void ThenNotPressed(string name) => Expect(Page.GetByRole(AriaRole.Button, new() { Name = name }).First, e => e.ToHaveAttributeAsync("aria-pressed", "false"));

    [Then("the {string} button is shown")]
    public void ThenButtonShown(string name) => Assert.True(Wait(Page.GetByRole(AriaRole.Button, new() { Name = name }).First), $"No button '{name}'. {Dump()}");

    [Then("the {string} button is not shown")]
    public void ThenButtonNotShown(string name) => Assert.True(Gone(Page.GetByRole(AriaRole.Button, new() { Name = name })), $"The button '{name}' is shown. {Dump()}");

    [Then("an alert says {string}")]
    public void ThenAlert(string text) => Assert.True(Wait(Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = text }).First), $"No alert with '{text}'. {Dump()}");

    [Then("the heading {string} is shown")]
    public void ThenHeading(string name) => Assert.True(Wait(Page.GetByRole(AriaRole.Heading, new() { Name = name, Level = 1 }).First), $"No heading '{name}'. {Dump()}");

    [Then("the {string} field has the value {string}")]
    public void ThenFieldValue(string label, string value) => Expect(Page.GetByLabel(label, new() { Exact = true }), e => e.ToHaveValueAsync(value));

    [Then("the text {string} is highlighted")]
    public void ThenHighlighted(string text) => Assert.True(Wait(Page.Locator("mark", new() { HasText = text }).First), $"'{text}' is not highlighted. {Dump()}");

    [Then("the browser address contains {string}")]
    public void ThenAddressContains(string text) => Assert.True(Poll(() => Page.Url.Contains(text, StringComparison.Ordinal)), $"The address is {Page.Url}");

    [Then("the browser address does not contain {string}")]
    public void ThenAddressDoesNotContain(string text) => Assert.True(Poll(() => !Page.Url.Contains(text, StringComparison.Ordinal)), $"The address is {Page.Url}");

    [When("I press the key {string}")]
    [Given("I press the key {string}")]
    public void WhenIPressTheKey(string key) => Page.Keyboard.PressAsync(key).GetAwaiter().GetResult();

    [Then("the focus is on the item {string}")]
    public void ThenFocusOnItem(string text) =>
        Assert.True(
            Poll(() => Page.EvaluateAsync<string>("() => (document.activeElement && document.activeElement.hasAttribute('data-nav')) ? document.activeElement.innerText : ''").GetAwaiter().GetResult().Contains(text, StringComparison.Ordinal)),
            $"The focus is not on an item with '{text}'. {Dump()}");

    [Then("the focus is in the search field")]
    public void ThenFocusInSearch() =>
        Assert.True(
            Poll(() => Page.EvaluateAsync<bool>("() => document.activeElement?.getAttribute('aria-label') === 'Search your library'").GetAwaiter().GetResult()),
            $"The search field has no focus. {Dump()}");

    [Then("the link {string} points to an address containing {string}")]
    public void ThenLinkPoints(string name, string text) =>
        Expect(Page.GetByRole(AriaRole.Link, new() { Name = name }).First, e => e.ToHaveAttributeAsync("href", new Regex(Regex.Escape(text))));

    [Then("the link {string} shows the number {int}")]
    public void ThenLinkShowsNumber(string name, int number) =>
        Expect(Page.GetByRole(AriaRole.Link, new() { Name = name }).First, e => e.ToContainTextAsync(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Then("the header shows the shelf version")]
    public void ThenVersionShown() =>
        Expect(Page.GetByLabel("Version").First, e => e.ToHaveTextAsync(new Regex(@"^v\d+\.\d+\.\d+")));

    // ---- lists of books and results ----

    private ILocator Items(string list) => Page.Locator($"[aria-label='{list}'] > li");

    [Then("the list {string} has {int} items")]
    public void ThenListCount(string list, int count) => Expect(Items(list), e => e.ToHaveCountAsync(count));

    [Then("the first item of the list {string} is {string}")]
    public void ThenFirstItem(string list, string title) => Expect(Items(list).First, e => e.ToContainTextAsync(title));

    [Then("the row {string} of the list {string} shows {string}")]
    public void ThenRowShows(string title, string list, string text) =>
        Expect(Items(list).Filter(new() { HasText = title }).First, e => e.ToContainTextAsync(text));

    [Then("the book {string} shows its cover")]
    public void ThenBookCover(string title)
    {
        var scope = Page.Locator("li, .cover-box").Filter(new() { HasText = title });
        var image = Page.Url.Contains("/works/", StringComparison.Ordinal) ? Page.Locator("img[src*='/cover']") : scope.Locator("img[src*='/cover']");
        Assert.True(Wait(image.First), $"No cover for '{title}'. {Dump()}");
    }

    [Then("the book {string} shows a spine instead of a cover")]
    public void ThenBookSpine(string title) =>
        Assert.True(Wait(Page.Locator("li").Filter(new() { HasText = title }).Locator("[data-spine]").First), $"No spine for '{title}'. {Dump()}");

    [Then("the book {string} shows its author {string}")]
    public void ThenBookAuthor(string title, string author) =>
        Expect(Page.Locator("li").Filter(new() { HasText = title }).First, e => e.ToContainTextAsync(author));

    [Then("the books of the list {string} stand in {int} row(s)")]
    public void ThenRows(string list, int rows)
    {
        Assert.True(Poll(() =>
        {
            var tops = Page.Locator($"[aria-label='{list}'] > li").EvaluateAllAsync<int[]>("items => items.map(i => Math.round(i.getBoundingClientRect().top))").GetAwaiter().GetResult();
            return tops.Distinct().Count() == rows;
        }), $"The books do not stand in {rows} rows. {Dump()}");
    }

    // ---- layout ----

    [Then("the {string} panel is visible")]
    public void ThenPanelVisible(string name) => Assert.True(Wait(Page.Locator($"[aria-label='{name}']").First), $"The panel '{name}' is not visible.");

    [Then("the {string} panel is hidden")]
    public void ThenPanelHidden(string name) => Assert.True(Gone(Page.Locator($"[aria-label='{name}']")), $"The panel '{name}' is visible.");

    [Then("the page has no sideways scroll")]
    public void ThenNoSidewaysScroll()
    {
        Page.WaitForLoadStateAsync(LoadState.NetworkIdle).GetAwaiter().GetResult();
        var widths = Page.EvaluateAsync<int[]>("() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]").GetAwaiter().GetResult();
        Assert.True(widths[0] <= widths[1], $"The page is {widths[0]} px wide in a window of {widths[1]} px.");
    }

    [Then("the page background is {word}")]
    public void ThenBackground(string tone)
    {
        var rgb = Page.EvaluateAsync<int[]>("() => getComputedStyle(document.body).backgroundColor.match(/\\d+/g).slice(0, 3).map(Number)").GetAwaiter().GetResult();
        var luminance = ((0.299 * rgb[0]) + (0.587 * rgb[1]) + (0.114 * rgb[2])) / 255;
        Assert.True(tone == "dark" ? luminance < 0.35 : luminance > 0.65, $"The background {string.Join(',', rgb)} is not {tone}.");
    }

    // ---- the reader ----

    /// <summary>The text of the page and of every frame in it (the pages of a book are frames; a closed shadow root hides them from selectors).</summary>
    private string AllText() =>
        string.Join('\n', Page.Frames.Select(f =>
        {
            try
            {
                return f.InnerTextAsync("body", new() { Timeout = 1000 }).GetAwaiter().GetResult();
            }
            catch (PlaywrightException)
            {
                return string.Empty;
            }
        }));

    [Then("the reader shows the text {string}")]
    [Given("the reader shows the text {string}")]
    public void ThenReaderShows(string text) => Assert.True(Poll(() => AllText().Contains(text, StringComparison.Ordinal)), $"The reader does not show '{text}'. {Dump()} Frames: {AllText().ReplaceLineEndings(" | ")}");

    [Then("the reader is at page {int} of {int}")]
    [Given("the reader is at page {int} of {int}")]
    public void ThenReaderAtPage(int page, int pages) => Expect(Page.GetByText($"Page {page} of {pages}", new() { Exact = true }), e => e.ToBeVisibleAsync());

    [Then("the reader highlights {string}")]
    public void ThenReaderHighlights(string text) => Assert.True(Wait(Page.Locator(".textLayer .highlight", new() { HasText = text }).First), $"Nothing is highlighted. {Dump()}");

    /// <summary>
    /// Selects the first occurrence of the text in a page of the book, as a person with the mouse would: the selection lives in the
    /// frame of the page, and the reader hears of it through <c>selectionchange</c>.
    /// </summary>
    [When("I select the text {string} in the book")]
    [Given("I select the text {string} in the book")]
    public void WhenISelectInBook(string text)
    {
        const string script = """
            (text) => {
              const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
              for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                const at = node.data.indexOf(text);
                if (at >= 0) {
                  const range = document.createRange();
                  range.setStart(node, at);
                  range.setEnd(node, at + text.length);
                  const selection = getSelection();
                  selection.removeAllRanges();
                  selection.addRange(range);
                  return true;
                }
              }
              return false;
            }
            """;
        Assert.True(Poll(() => Page.Frames.Where(f => f != Page.MainFrame).Any(f =>
        {
            try
            {
                return f.EvaluateAsync<bool>(script, text).GetAwaiter().GetResult();
            }
            catch (PlaywrightException)
            {
                return false;
            }
        })), $"'{text}' is not in a page of the book. {Dump()}");
    }

    /// <summary>Selects the first occurrence of the text in the text layer of a PDF page, as a person with the mouse would.</summary>
    [When("I select the text {string} on the PDF page")]
    [Given("I select the text {string} on the PDF page")]
    public void WhenISelectOnPdf(string text)
    {
        const string script = """
            (text) => {
              const walker = document.createTreeWalker(document.querySelector('.pdfViewer'), NodeFilter.SHOW_TEXT);
              for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                const at = node.data.indexOf(text);
                if (at >= 0 && node.parentElement.closest('.textLayer')) {
                  const range = document.createRange();
                  range.setStart(node, at);
                  range.setEnd(node, at + text.length);
                  const selection = getSelection();
                  selection.removeAllRanges();
                  selection.addRange(range);
                  return true;
                }
              }
              return false;
            }
            """;
        Assert.True(Poll(() => Page.EvaluateAsync<bool>(script, text).GetAwaiter().GetResult()), $"'{text}' is not in the text layer of a page. {Dump()}");
    }

    [Then("the PDF page shows {int} highlight(s)")]
    public void ThenPdfShowsHighlights(int count) =>
        Assert.True(Poll(() => Page.Locator(".shelf-highlight").CountAsync().GetAwaiter().GetResult() == count), $"The pages do not show {count} highlights. {Dump()}");

    [Then("the book shows {int} highlight(s)")]
    public void ThenBookShowsHighlights(int count) =>
        Expect(Page.Locator("[aria-label='Book']"), e => e.ToHaveAttributeAsync("data-highlights", count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Then("the rendered note shows {string} in bold")]
    public void ThenRenderedNoteBold(string text) => Assert.True(Wait(Page.Locator("aside[aria-label='Notes'] strong", new() { HasText = text }).First), $"No bold '{text}' in the notes. {Dump()}");

    [Then("the title of the page is not {string}")]
    public void ThenTitleIsNot(string title) => Assert.NotEqual(title, Page.TitleAsync().GetAwaiter().GetResult());

    [Then("the browser reported no errors")]
    public void ThenNoErrors()
    {
        Page.WaitForLoadStateAsync(LoadState.NetworkIdle).GetAwaiter().GetResult();
        Assert.True(_browserErrors.Count == 0, $"The browser reported: {string.Join(" | ", _browserErrors)}");
    }

    [Then("the browser reported {string}")]
    public void ThenReported(string text) => Assert.True(Poll(() => _browserErrors.Any(e => e.Contains(text, StringComparison.Ordinal))), $"No report with '{text}'. The browser said: {string.Join(" | ", _browserErrors)}");

    [Then("the panel {string} shows the entry {string}")]
    public void ThenPanelEntry(string panel, string text) => Assert.True(Wait(Page.Locator($"aside[aria-label='{panel}']").GetByText(text).First), $"The panel '{panel}' has no '{text}'. {Dump()}");

    [When("I click the entry {string} in the panel {string}")]
    public void WhenClickEntry(string text, string panel) => Page.Locator($"aside[aria-label='{panel}']").GetByText(text).First.ClickAsync().GetAwaiter().GetResult();

    [When("I click the {string} button {int} times")]
    public void WhenClickTimes(string name, int times)
    {
        var button = Page.GetByRole(AriaRole.Button, new() { Name = name }).First;
        for (var i = 0; i < times; i++)
        {
            if (!button.IsEnabledAsync().GetAwaiter().GetResult())
            {
                break;
            }

            button.ClickAsync().GetAwaiter().GetResult();
        }
    }

    [Then("the {string} button is disabled")]
    public void ThenButtonDisabled(string name) => Expect(Page.GetByRole(AriaRole.Button, new() { Name = name }).First, e => e.ToBeDisabledAsync());

    private double _noted;

    private double PageWidth() =>
        Page.Locator(".pdfViewer .page").First.EvaluateAsync<double>("e => e.getBoundingClientRect().width").GetAwaiter().GetResult();

    [Given("I note the width of the PDF page")]
    public void GivenNoteWidth() => _noted = PageWidth();

    [Then("the PDF page is wider than noted")]
    public void ThenWider() => Assert.True(Poll(() => PageWidth() > _noted + 1), $"The page is {PageWidth()} px wide, it was {_noted}.");

    [Then("the PDF page is narrower than the width of the window")]
    public void ThenNarrower() => Assert.True(Poll(() => PageWidth() < _viewport.Width), $"The page is {PageWidth()} px wide in a window of {_viewport.Width}.");

    [Then("the page of the book is {word}")]
    public void ThenBookPage(string tone)
    {
        Assert.True(Poll(() =>
        {
            foreach (var frame in Page.Frames.Where(f => f != Page.MainFrame))
            {
                try
                {
                    var rgb = frame.EvaluateAsync<int[]>("() => getComputedStyle(document.body).backgroundColor.match(/\\d+/g).slice(0, 3).map(Number)").GetAwaiter().GetResult();
                    var luminance = ((0.299 * rgb[0]) + (0.587 * rgb[1]) + (0.114 * rgb[2])) / 255;
                    return tone == "dark" ? luminance < 0.35 : luminance > 0.65;
                }
                catch (PlaywrightException)
                {
                    // A frame that is not a page of the book.
                }
            }

            return false;
        }), $"The page of the book is not {tone}. {Dump()}");
    }

    [Then("the text in the book is {int} percent")]
    [Given("the text in the book is {int} percent")]
    public void ThenTextSize(int percent) =>
        Assert.True(Poll(() => Page.Frames.Where(f => f != Page.MainFrame).Any(f =>
        {
            try
            {
                return Math.Abs(f.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.documentElement).fontSize) / 16 * 100").GetAwaiter().GetResult() - percent) < 1;
            }
            catch (PlaywrightException)
            {
                return false;
            }
        })), $"The text in the book is not {percent} percent. {Dump()}");

    [Then("the script of the book did not run")]
    public void ThenScriptDidNotRun() =>
        Assert.True(Page.Frames.All(f =>
        {
            try
            {
                return f.EvaluateAsync<bool>("() => !document.documentElement.hasAttribute('data-script-ran')").GetAwaiter().GetResult();
            }
            catch (PlaywrightException)
            {
                return true;
            }
        }), "The script of the book ran: a frame carries data-script-ran.");

    [When("I go back in the browser")]
    public void WhenBack() => Page.GoBackAsync().GetAwaiter().GetResult();

    [When("I open the current address in a new browser")]
    [Given("I open the current address in a new browser")]
    public void WhenOpenInNew()
    {
        var address = Page.Url;
        _context?.CloseAsync().GetAwaiter().GetResult();
        Page = NewPage();
        Page.GotoAsync(address).GetAwaiter().GetResult();
    }

    [Given("an EPUB {string} whose chapter runs a script that marks the page")]
    public void GivenAnEvilEpub(string path)
    {
        var target = Path.Combine(workspace.WorkingDirectory, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(CoverSteps.FixturePath(Path.Combine("derived", "epub3-isbn-series.epub")), target);
        using var archive = System.IO.Compression.ZipFile.Open(target, System.IO.Compression.ZipArchiveMode.Update);
        var chapter = archive.Entries.First(e => e.FullName.EndsWith("chapter-01.xhtml", StringComparison.Ordinal));
        string text;
        using (var reader = new StreamReader(chapter.Open()))
        {
            text = reader.ReadToEnd();
        }

        var marked = text.Replace("<body>", "<body><p>The chapter is shown</p><script>document.documentElement.setAttribute('data-script-ran', 'yes')</script><img src=\"https://example.invalid/pixel.png\" alt=\"\"/>", StringComparison.Ordinal);
        Assert.NotEqual(text, marked);
        var name = chapter.FullName;
        chapter.Delete();
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(marked);
    }

    // ---- duplicates ----

    private ILocator Card(string file) => Page.GetByRole(AriaRole.Article, new() { Name = file, Exact = true });

    [Then("the file card {string} is marked as the suggestion to keep")]
    public void ThenSuggested(string file) => Expect(Card(file), e => e.ToHaveAttributeAsync("data-suggested", "true"));

    [Then("the file card {string} is not marked as the suggestion to keep")]
    public void ThenNotSuggested(string file) => Expect(Card(file), e => e.ToHaveAttributeAsync("data-suggested", "false"));

    [When("I click the {string} button of the file card {string}")]
    [Given("I click the {string} button of the file card {string}")]
    public void WhenCardButton(string button, string file) => Card(file).GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync().GetAwaiter().GetResult();

    [Then("the file card {string} has a {string} button")]
    public void ThenCardHasButton(string file, string button) => Assert.True(Wait(Card(file).GetByRole(AriaRole.Button, new() { Name = button, Exact = true })), $"'{file}' has no '{button}' button. {Dump()}");

    [Then("the file card {string} has no {string} button")]
    public void ThenCardHasNoButton(string file, string button) => Assert.True(Gone(Card(file).GetByRole(AriaRole.Button, new() { Name = button, Exact = true })), $"'{file}' has a '{button}' button. {Dump()}");

    [Then("the file card {string} asks to confirm")]
    public void ThenAsksToConfirm(string file) => Assert.True(Wait(Card(file).GetByRole(AriaRole.Button, new() { Name = "Confirm" })), $"'{file}' does not ask. {Dump()}");

    [Then("the file card {string} does not ask to confirm")]
    public void ThenDoesNotAsk(string file) => Assert.True(Gone(Card(file).GetByRole(AriaRole.Button, new() { Name = "Confirm" })), $"'{file}' asks. {Dump()}");

    [When("I click the {string} button of the group {string} with the file {string}")]
    [Given("I click the {string} button of the group {string} with the file {string}")]
    public void WhenGroupButton(string button, string group, string file) =>
        Page.Locator("section.group").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = group, Exact = true }) }).Filter(new() { Has = Card(file) })
            .GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync().GetAwaiter().GetResult();

    private IPage NewPage()
    {
        _context?.CloseAsync().GetAwaiter().GetResult();
        _contextKey = (_dark, _viewport);
        _context = Browser().NewContextAsync(new BrowserNewContextOptions
        {
            ColorScheme = _dark ? ColorScheme.Dark : ColorScheme.Light,
            ViewportSize = new ViewportSize { Width = _viewport.Width, Height = _viewport.Height },
            Locale = "en-US",
        }).GetAwaiter().GetResult();
        var page = _context.NewPageAsync().GetAwaiter().GetResult();
        page.SetDefaultTimeout(Timeout);
        page.PageError += (_, message) => _browserErrors.Add(message);
        page.Response += (_, response) => _traffic.Add($"{response.Status} {response.Request.Method} {new Uri(response.Url).PathAndQuery}{(response.Headers.TryGetValue("set-cookie", out var cookie) ? " set-cookie: " + cookie : string.Empty)}");
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                _browserErrors.Add(message.Text);
            }
        };
        return page;
    }

    /// <summary>Waits until the locator's assertion holds (Playwright retries it) and turns a timeout into a failure with the page text.</summary>
    private void Expect(ILocator locator, Func<ILocatorAssertions, Task> assertion)
    {
        try
        {
            assertion(Assertions.Expect(locator)).GetAwaiter().GetResult();
        }
        catch (PlaywrightException e)
        {
            Assert.Fail($"{e.Message.Split('\n')[0]} {Dump()}");
        }
    }

    /// <summary>True when nothing that matches the locator is visible (within the time Playwright allows for the page to settle).</summary>
    private static bool Gone(ILocator locator) => Poll(() => !locator.AllAsync().GetAwaiter().GetResult().Any(l => l.IsVisibleAsync().GetAwaiter().GetResult()));

    private static bool Poll(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(Timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    private static bool Wait(ILocator locator)
    {
        try
        {
            locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = Timeout }).GetAwaiter().GetResult();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private string Dump()
    {
        var text = Page.InnerTextAsync("body").GetAwaiter().GetResult();
        var requests = Page.EvaluateAsync<string[]>("() => performance.getEntriesByType('resource').map(e => e.name).filter(n => n.includes('/api/'))").GetAwaiter().GetResult();
        return $"The page ({Page.Url}) says: {text.ReplaceLineEndings(" | ")} The API was asked: {string.Join(" ; ", requests.Select(r => new Uri(r).PathAndQuery))} The server said: {web.ServerErrors.ReplaceLineEndings(" | ")} The browser said: {string.Join(" | ", _browserErrors)} Traffic: {string.Join(" ; ", _traffic)} Cookies: {string.Join(" ; ", _context!.CookiesAsync().GetAwaiter().GetResult().Select(c => $"{c.Name}={c.Value} path={c.Path} domain={c.Domain} sameSite={c.SameSite} httpOnly={c.HttpOnly}"))}";
    }

    /// <summary>With SHELF_SCREENSHOTS set to a folder, the last page of every browser scenario is saved there, for a look at the design.</summary>
    // The page must be closed before the server stops and before the working directory is deleted (the default order of the other
    // hooks): a page that is still open keeps asking the server, and the server keeps writing into the folder that is being removed.
    [AfterScenario(Order = 100)]
    public void FinishScenario(ScenarioContext scenario)
    {
        var folder = Environment.GetEnvironmentVariable("SHELF_SCREENSHOTS");
        if (!string.IsNullOrEmpty(folder) && Page is not null)
        {
            Directory.CreateDirectory(folder);
            var name = string.Concat(scenario.ScenarioInfo.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
            Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(folder, $"{name[..Math.Min(name.Length, 80)]}.png"), FullPage = true }).GetAwaiter().GetResult();
        }

        _context?.CloseAsync().GetAwaiter().GetResult();
        _context = null;
    }

    public void Dispose() => _context?.CloseAsync().GetAwaiter().GetResult();
}
