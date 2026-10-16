using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SlidePlayer : UdonSharpBehaviour
{
    [Header("URL generation (editor only)")]
    [Tooltip("Url for the folder where the slides, without a trailing slash. Example: https://example.com/slides")]
    public string baseUrl = "";

    [Tooltip("How many slide URLs to generate. https://example.com/slides/Slide1.png https://example.com/slides/Slide2.png...")]
    public int slideCount = 40;

    [Tooltip("Text in front of the number in each file name. Example: Slide")]
    public string filePrefix = "Slide";

    [Tooltip("Extension used for every generated URL. Example: .jpg")]
    public string fileExtension = ".jpg";

    [Header("Generated URLs")]
    [Tooltip("Filled in by the Generate button. Udon cannot build these at runtime.")]
    public VRCUrl[] slides;

    [Header("Output")]
    [Tooltip("The screen gameobject with the material to render slides to.")]
    public Renderer screen;
    [Tooltip("Buttons that control the slides.")]
    public SlideButton[] buttons;
    [Tooltip("Text field that shows error messages in the world. Can be left empty.")]
    public Text errorLog;
    [Tooltip("Text field that shows the current slide number. Can be left empty.")]
    public Text slideNumberText;
    [Tooltip("Text field that shows how many slides are ready while they load. Can be left empty.")]
    public Text progressText;

    [Header("Memory")]
    [Tooltip("Compress each slide after it loads. On Quest a 1280x720 slide use 1.2 MB instead of 4.9 MB.")]
    public bool compressSlides = true;

    // VRChat allows one image download every 5 seconds. Extra requests are queued and run in random order.
    private const float SecondsBetweenLoads = 5f;

    private const int MaxLogLines = 8;

    // The current slide. It is synced, so a late joiner will see the same slide as everyone else.
    [UdonSynced] private int _sliderNr;

    private VRCImageDownloader _loader;
    private TextureInfo _textureInfo;
    private bool _loading;
    private int _loadingSlideNr;
    private int _shownSlideNr;
    private bool _pendingLoad;
    private bool _onCooldown;

    private string[] _logLines;
    private int _logLineCount;

    // Number of slides on the host. -1 until a download fails with 404.
    private int _deckLength;

    private Texture2D[] _cache;
    private int _cachedCount;
    private int _cacheBytes;

    // A failing slide would otherwise be retried every cooldown for the rest of the session.
    private bool _preloadStopped;

    void Start()
    {
        _logLines = new string[MaxLogLines];

        if (screen == null)
        {
            LogError("No screen assigned in inspector.");
        }

        if (slides == null || slides.Length == 0)
        {
            LogError("No slide URLs. Press Generate URLs in the inspector.");
            return;
        }

        _shownSlideNr = -1;
        _deckLength = -1;
        _cache = new Texture2D[slides.Length];
        _loader = new VRCImageDownloader();

        _textureInfo = new TextureInfo();
        _textureInfo.GenerateMipMaps = true;
        _textureInfo.WrapModeU = TextureWrapMode.Clamp;
        _textureInfo.WrapModeV = TextureWrapMode.Clamp;

        // Downloads write to a property that no shader reads. Display sets _MainTex from
        // the cache, so a download in progress never replaces the slide on screen.
        _textureInfo.MaterialProperty = "_SlideDownloadSlot";

        // Wait one frame, because Unity does not guarantee that each
        // SlideButton.Start runs before this Start method.

        SendCustomEventDelayedFrames(nameof(TryLoadImage), 1);

        RefreshSlideNumberDisplay();
    }

    public bool CanStep()
    {
        return !_onCooldown && !_loading;
    }

    public void Next()
    {
        GoToSlide(NextIndex());
    }

    public void Prev()
    {
        GoToSlide(PrevIndex());
    }

    public void First()
    {
        GoToSlide(0);
    }

    public void Refresh()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(DoRefresh));
    }

    // Public because SendCustomNetworkEvent can only reach a public method.
    public void DoRefresh()
    {
        LogInfo("Refreshing the deck.");

        // dispose of the old downloaded textures so we have room for new ones.
        _loader.Dispose();
        _loader = new VRCImageDownloader();

        _cache = new Texture2D[slides.Length];
        _cachedCount = 0;
        _cacheBytes = 0;
        _deckLength = -1;
        _shownSlideNr = -1;
        _preloadStopped = false;
        _loading = false;

        screen.material.SetTexture("_MainTex", null);

        TryLoadImage();
        RefreshButtons();
    }

    private int NextIndex()
    {
        int next = _sliderNr + 1;

        if (_deckLength > 0 && next >= _deckLength)
        {
            next = 0;
        }

        if (next >= slides.Length)
        {
            next = 0;
        }

        return next;
    }

    private int PrevIndex()
    {
        int prev = _sliderNr - 1;

        if (prev < 0)
        {
            // The last slide number is still unknown, so there is nothing to wrap to.
            if (_deckLength <= 0) return _sliderNr;

            prev = _deckLength - 1;
        }

        return prev;
    }

    public override void OnDeserialization()
    {
        ShowOrLoad();
    }

    public override void OnImageLoadSuccess(IVRCImageDownload download)
    {
        _loading = false;
        _preloadStopped = false;
        StoreInCache(_loadingSlideNr, download.Result);

        if (IsCached(_sliderNr))
        {
            Display(_sliderNr);
        }
        else
        {
            _pendingLoad = true;
        }

        if (_pendingLoad)
        {
            TryLoadImage();
        }
        else
        {
            PreloadNext();
        }

        RefreshButtons();
    }

    public override void OnImageLoadError(IVRCImageDownload download)
    {
        _loading = false;

        if (download.Error == VRCImageDownloadError.TooManyRequests)
        {
            LogError("Slide " + (_loadingSlideNr + 1) + ": too many requests, retrying after the cooldown. "
                + slides[_loadingSlideNr].Get() + " - " + download.ErrorMessage);

            _pendingLoad = true;

            if (!_onCooldown)
            {
                StartCooldown();
            }

            RefreshButtons();
            return;
        }

        if (IsMissingSlide(download))
        {
            MarkEndOfDeck();
            RefreshButtons();
            return;
        }

        LogError("Slide " + (_loadingSlideNr + 1) + ": " + DescribeError(download.Error) + ". "
            + slides[_loadingSlideNr].Get() + " - " + download.ErrorMessage);

        _preloadStopped = true;

        ReturnToShownSlide();
        RefreshButtons();
    }

    private void StoreInCache(int index, Texture2D texture)
    {
        // Dispose destroys the textures. A download that finishes after a refresh returns a destroyed one.
        if (texture == null)
        {
            return;
        }
        
        if (compressSlides)
        {
            texture.Compress(true);
        }

        // If a texture is writable, a duplicate will Setting this to no longer readable removes the duplicate copy
        // in memory.
        texture.Apply(false, true);

        int bytes = TextureMemory.Bytes(texture);

        if (_cache[index] == null)
        {
            _cachedCount++;
            _cacheBytes = _cacheBytes + bytes;
        }

        _cache[index] = texture;

        LogInfo("Slide " + (index + 1) + ": " + (bytes / 1024)
            + " KB, deck total " + (_cacheBytes / 1024) + " KB");
    }

    private bool IsCached(int index)
    {
        return _cache[index] != null;
    }

    private void Display(int index)
    {
        screen.material.SetTexture("_MainTex", _cache[index]);
        _shownSlideNr = index;
        RefreshButtons();
    }

    private void ShowOrLoad()
    {
        if (IsCached(_sliderNr))
        {
            Display(_sliderNr);
            return;
        }

        TryLoadImage();
    }

    private bool IsMissingSlide(IVRCImageDownload download)
    {
        return download.Error == VRCImageDownloadError.DownloadError
            && download.ErrorMessage.Contains("404");
    }

    private void MarkEndOfDeck()
    {
        if (_loadingSlideNr == 0)
        {
            LogError("Slide 1 is missing, so the deck is empty. " + slides[0].Get());
            return;
        }

        _deckLength = _loadingSlideNr;
        LogInfo("The deck holds " + _deckLength + " slides.");

        ReturnToShownSlide();
    }

    private void ReturnToShownSlide()
    {
        if (_shownSlideNr < 0 || _sliderNr != _loadingSlideNr || _sliderNr == _shownSlideNr)
        {
            return;
        }

        _sliderNr = _shownSlideNr;

        if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
        {
            RequestSerialization();
        }
    }

    public void EndCooldown()
    {
        _onCooldown = false;
        RefreshButtons();

        if (_pendingLoad)
        {
            TryLoadImage();
            return;
        }

        PreloadNext();
    }

    private void PreloadNext()
    {
        if (_loading || _onCooldown || _preloadStopped) return;

        int index = NextSlideToPreload();

        if (index < 0)
        {
            return;
        }

        StartDownload(index);
    }

    private int NextSlideToPreload()
    {
        int limit = slides.Length;

        if (_deckLength > 0)
        {
            limit = _deckLength;
        }

        if (_cache[_sliderNr] == null)
        {
            return _sliderNr;
        }

        int previous = _sliderNr - 1;

        // Wrapping back before the deck length is known would probe a slide that does not exist.
        if (previous < 0 && _deckLength > 0)
        {
            previous = _deckLength - 1;
        }

        if (previous >= 0 && _cache[previous] == null)
        {
            return previous;
        }

        // Then walk forward and wrap, so the slides after the current one are cached first.
        for (int step = 1; step < limit; step++)
        {
            int index = (_sliderNr + step) % limit;

            if (_cache[index] == null)
            {
                return index;
            }
        }

        return -1;
    }

    private void GoToSlide(int newSlideNr)
    {
        if (newSlideNr == _sliderNr) return;

        // A cached slide needs no download, so the cooldown only blocks new downloads.
        if (!IsCached(newSlideNr) && !CanStep()) return;

        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        _sliderNr = newSlideNr;
        RequestSerialization();

        ShowOrLoad();
    }

    public void TryLoadImage()
    {
        // A remote client can ask for a load during the cooldown, so queue it instead.
        if (_loading || _onCooldown)
        {
            _pendingLoad = true;
            return;
        }

        _pendingLoad = false;
        StartDownload(_sliderNr);
    }

    private void StartDownload(int index)
    {
        _loading = true;
        _loadingSlideNr = index;

        StartCooldown();
        _loader.DownloadImage(slides[index], screen.material, this, _textureInfo);
    }

    private void StartCooldown()
    {
        _onCooldown = true;
        RefreshButtons();
        SendCustomEventDelayedSeconds(nameof(EndCooldown), SecondsBetweenLoads);
    }

    public void LogButtonError(string message)
    {
        LogError(message);
    }

    private void LogError(string message)
    {
        Debug.LogError("[SlidePlayer] " + message);
        ShowLogLine(message);
    }

    private void LogInfo(string message)
    {
        Debug.Log("[SlidePlayer] " + message);
        ShowLogLine(message);
    }

    private void ShowLogLine(string message)
    {
        if (errorLog == null)
        {
            return;
        }

        if (_logLines == null)
        {
            _logLines = new string[MaxLogLines];
        }

        if (_logLineCount == MaxLogLines)
        {
            for (int i = 0; i < MaxLogLines - 1; i++)
            {
                _logLines[i] = _logLines[i + 1];
            }

            _logLineCount--;
        }

        _logLines[_logLineCount] = message;
        _logLineCount++;

        string text = "";
        for (int i = 0; i < _logLineCount; i++)
        {
            text = text + _logLines[i] + "\n";
        }

        errorLog.text = text;
    }

    private string DescribeError(VRCImageDownloadError error)
    {
        if (error == VRCImageDownloadError.InvalidURL)
        {
            return "the URL is not valid";
        }

        if (error == VRCImageDownloadError.AccessDenied)
        {
            return "access denied, untrusted URLs may be turned off";
        }

        if (error == VRCImageDownloadError.InvalidImage)
        {
            return "the file is not an image VRChat can read";
        }

        if (error == VRCImageDownloadError.DownloadError)
        {
            return "the download failed, the host may be down";
        }

        if (error == VRCImageDownloadError.TooManyRequests)
        {
            return "too many requests";
        }

        return "unknown error";
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].SetAvailable(CanReach(buttons[i].action));
        }

        RefreshSlideNumberDisplay();
        RefreshProgressDisplay();
    }

    private bool CanReach(SlideButtonAction action)
    {
        if (action == SlideButtonAction.Refresh)
        {
            return true;
        }

        int target = 0;

        if (action == SlideButtonAction.Next)
        {
            target = NextIndex();
        }
        else if (action == SlideButtonAction.Previous)
        {
            target = PrevIndex();
        }

        return IsCached(target) || CanStep();
    }

    private void RefreshSlideNumberDisplay()
    {
        if (slideNumberText == null)
        {
            return;
        }

        slideNumberText.text = "Slide " + (_sliderNr + 1);
    }

    private void RefreshProgressDisplay()
    {
        if (progressText == null)
        {
            return;
        }

        if (IsPreloadDone())
        {
            progressText.text = "";
            return;
        }

        // The host does not report the deck length, so only the ready count is known.
        progressText.text = "Loading slides: " + _cachedCount + " ready";
    }

    private bool IsPreloadDone()
    {
        if (_preloadStopped) return true;
        if (_cachedCount >= slides.Length) return true;

        return _deckLength > 0 && _cachedCount >= _deckLength;
    }
}
