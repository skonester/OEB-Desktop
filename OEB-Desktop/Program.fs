open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Threading.Tasks
open System.Speech.Synthesis
open System.Windows.Forms
open System.Drawing
open KokoroSharp
open KokoroSharp.Core
open KokoroSharp.Processing

type Chapter = {
    Number: int
    DisplayText: string
    SpeechText: string
}

type Book = {
    Name: string
    Chapters: Chapter list
}

type TreeNodeTag =
    | BookNode of Book
    | ChapterNode of Book * Chapter
    | MissingBookNode of string

let canonicalBookNames = [
    "Genesis"; "Exodus"; "Leviticus"; "Numbers"; "Deuteronomy"; "Joshua"; "Judges"; "Ruth"
    "1 Samuel"; "2 Samuel"; "1 Kings"; "2 Kings"; "1 Chronicles"; "2 Chronicles"; "Ezra"; "Nehemiah"; "Esther"
    "Job"; "Psalms"; "Proverbs"; "Ecclesiastes"; "Song of Songs"; "Isaiah"; "Jeremiah"; "Lamentations"; "Ezekiel"; "Daniel"
    "Hosea"; "Joel"; "Amos"; "Obadiah"; "Jonah"; "Micah"; "Nahum"; "Habakkuk"; "Zephaniah"; "Haggai"; "Zechariah"; "Malachi"
    "Matthew"; "Mark"; "Luke"; "John"; "Acts"; "Romans"; "1 Corinthians"; "2 Corinthians"; "Galatians"; "Ephesians"; "Philippians"; "Colossians"
    "1 Thessalonians"; "2 Thessalonians"; "1 Timothy"; "2 Timothy"; "Titus"; "Philemon"; "Hebrews"; "James"; "1 Peter"; "2 Peter"
    "1 John"; "2 John"; "3 John"; "Jude"; "Revelation"
]

let bibleSections = [
    "Pentateuch", ["Genesis"; "Exodus"; "Leviticus"; "Numbers"; "Deuteronomy"]
    "History", ["Joshua"; "Judges"; "Ruth"; "1 Samuel"; "2 Samuel"; "1 Kings"; "2 Kings"; "1 Chronicles"; "2 Chronicles"; "Ezra"; "Nehemiah"; "Esther"]
    "Poetry", ["Job"; "Psalms"; "Proverbs"; "Ecclesiastes"; "Song of Songs"]
    "Major Prophets", ["Isaiah"; "Jeremiah"; "Lamentations"; "Ezekiel"; "Daniel"]
    "Minor Prophets", ["Hosea"; "Joel"; "Amos"; "Obadiah"; "Jonah"; "Micah"; "Nahum"; "Habakkuk"; "Zephaniah"; "Haggai"; "Zechariah"; "Malachi"]
    "Gospels and Acts", ["Matthew"; "Mark"; "Luke"; "John"; "Acts"]
    "Letters", ["Romans"; "1 Corinthians"; "2 Corinthians"; "Galatians"; "Ephesians"; "Philippians"; "Colossians"; "1 Thessalonians"; "2 Thessalonians"; "1 Timothy"; "2 Timothy"; "Titus"; "Philemon"; "Hebrews"; "James"; "1 Peter"; "2 Peter"; "1 John"; "2 John"; "3 John"; "Jude"]
    "Apocalypse", ["Revelation"]
]

let azureNeuralVoices = [
    "en-US-JennyNeural"
    "en-US-GuyNeural"
    "en-US-AriaNeural"
    "en-US-DavisNeural"
    "en-US-ChristopherNeural"
    "en-US-ElizabethNeural"
    "en-GB-SoniaNeural"
    "en-GB-RyanNeural"
]

let kokoroOfflineVoices = [
    "af_heart"; "af_bella"; "af_nicole"; "af_sarah"; "af_sky"
    "am_michael"; "am_fenrir"; "am_puck"
    "bf_emma"; "bf_isabella"; "bm_george"; "bm_fable"
]

let appendLine (builder: StringBuilder) (text: string) =
    builder.AppendLine(text) |> ignore

let appendBlankLine (builder: StringBuilder) =
    if builder.Length > 0 then
        builder.AppendLine() |> ignore

let appendSpeech (builder: StringBuilder) (text: string) =
    let trimmed = text.Trim()
    if trimmed.Length > 0 then
        if builder.Length > 0 then builder.Append(" ") |> ignore
        builder.Append(trimmed) |> ignore

let cleanUsfmText (text: string) =
    text
    |> fun value -> Regex.Replace(value, @"\\[a-zA-Z0-9]+\*", "")
    |> fun value -> Regex.Replace(value, @"\\[a-zA-Z0-9]+\s*", "")
    |> fun value -> Regex.Replace(value, @"\s+", " ")
    |> fun value -> value.Trim()

let chapterNumberFromLine (line: string) =
    let m = Regex.Match(line, @"^\\c\s+(\d+)")
    if m.Success then Some(Int32.Parse(m.Groups.[1].Value)) else None

let parseUsfmBook (path: string) =
    let lines = File.ReadAllLines(path, Encoding.UTF8)
    let chapters = ResizeArray<Chapter>()
    let fileName =
        match Path.GetFileNameWithoutExtension(path) with
        | null -> ""
        | value -> value
    let mutable bookName =
        if String.IsNullOrWhiteSpace(fileName) then "Unknown Book"
        elif fileName.Length > 3 then fileName.Substring(3)
        else fileName
    let mutable hasHeaderName = false
    let mutable currentChapter = 0
    let display = StringBuilder()
    let speech = StringBuilder()

    let finishChapter () =
        if currentChapter > 0 then
            let displayText = display.ToString().Trim()
            let speechText = speech.ToString().Trim()
            if displayText.Length > 0 || speechText.Length > 0 then
                chapters.Add({
                    Number = currentChapter
                    DisplayText = displayText
                    SpeechText = speechText
                })
            display.Clear() |> ignore
            speech.Clear() |> ignore

    for rawLine in lines do
        let line = rawLine.TrimEnd()
        match chapterNumberFromLine line with
        | Some chapterNumber ->
            finishChapter()
            currentChapter <- chapterNumber
        | None ->
            let markerMatch = Regex.Match(line, @"^\\([a-zA-Z0-9]+)\s*(.*)$")
            if markerMatch.Success then
                let marker = markerMatch.Groups.[1].Value
                let content = cleanUsfmText markerMatch.Groups.[2].Value

                if currentChapter = 0 then
                    if marker = "h" && content.Length > 0 then
                        bookName <- content
                        hasHeaderName <- true
                    elif not hasHeaderName && marker.StartsWith("mt") && content.Length > 0 then
                        bookName <- content
                else
                    match marker with
                    | "v" ->
                        let verse = Regex.Match(content, @"^(\d+[a-z]?)\s*(.*)$")
                        if verse.Success then
                            let verseNumber = verse.Groups.[1].Value
                            let verseText = verse.Groups.[2].Value.Trim()
                            if verseText.Length > 0 then
                                appendLine display (sprintf "%s  %s" verseNumber verseText)
                                appendSpeech speech verseText
                        elif content.Length > 0 then
                            appendLine display content
                            appendSpeech speech content
                    | marker when marker.StartsWith("s") ->
                        if content.Length > 0 then
                            appendBlankLine display
                            appendLine display content
                            appendBlankLine display
                            appendSpeech speech content
                    | "p" | "m" | "q" | "q1" | "q2" | "q3" | "pi" | "pi1" | "pi2" ->
                        if content.Length > 0 then
                            let indent =
                                if marker.StartsWith("q") then "    "
                                elif marker.StartsWith("pi") then "  "
                                else ""
                            appendLine display (indent + content)
                            appendSpeech speech content
                        else
                            appendBlankLine display
                    | "b" ->
                        appendBlankLine display
                    | _ ->
                        if content.Length > 0 && not (marker = "rem") then
                            appendLine display content
                            appendSpeech speech content
            elif currentChapter > 0 && line.Trim().Length > 0 then
                let text = cleanUsfmText line
                appendLine display text
                appendSpeech speech text

    finishChapter()
    { Name = bookName; Chapters = Seq.toList chapters }

let loadBooks (root: string) =
    let candidates = [
        Path.Combine(root, "artifacts", "us", "usfm")
        Path.Combine(root, "artifacts", "us-release", "usfm")
        Path.Combine(root, "artifacts", "cth", "usfm")
    ]

    candidates
    |> List.tryFind Directory.Exists
    |> function
        | None -> []
        | Some usfmPath ->
            Directory.GetFiles(usfmPath, "*.usfm")
            |> Array.sort
            |> Array.choose (fun path ->
                try
                    let book = parseUsfmBook path
                    if List.isEmpty book.Chapters then None else Some book
                with _ ->
                    None)
            |> Array.toList

[<STAThread>]
[<EntryPoint>]
let main argv =
    let logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_log.txt")
    let writeLog message =
        try
            File.AppendAllText(logPath, sprintf "[%s] %s\n" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) message)
        with _ -> ()

    try
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(false)

        let form = new Form(Width = 1120, Height = 780, Text = "Open English Bible Reader")
        form.BackColor <- Color.FromArgb(24, 24, 27)
        form.ForeColor <- Color.FromArgb(244, 244, 245)
        form.StartPosition <- FormStartPosition.CenterScreen

        let topBar = new Panel(Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(24, 24, 27))
        let leftPanel = new TableLayoutPanel(Dock = DockStyle.Left, Width = 300, BackColor = Color.FromArgb(24, 24, 27))
        leftPanel.ColumnCount <- 1
        leftPanel.RowCount <- 2
        leftPanel.RowStyles.Add(RowStyle(SizeType.Absolute, 54.0f)) |> ignore
        leftPanel.RowStyles.Add(RowStyle(SizeType.Percent, 100.0f)) |> ignore

        let searchPanel = new Panel(Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 27))

        let searchLabel = new Label(
            Text = "Search",
            Location = Point(12, 17),
            ForeColor = Color.FromArgb(161, 161, 170),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true)

        let searchBox = new TextBox(
            Location = Point(76, 13),
            Width = 205,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.FromArgb(244, 244, 245),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10.0f))

        let treeView = new TreeView(
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 32, 35),
            ForeColor = Color.FromArgb(244, 244, 245),
            LineColor = Color.FromArgb(82, 82, 91),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10.5f),
            HideSelection = false,
            ShowLines = false,
            FullRowSelect = true)

        searchPanel.Controls.Add(searchLabel) |> ignore
        searchPanel.Controls.Add(searchBox) |> ignore
        leftPanel.Controls.Add(searchPanel, 0, 0)
        leftPanel.Controls.Add(treeView, 0, 1)

        let contentPanel = new Panel(Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 18))
        contentPanel.Padding <- Padding(28)

        let contentBox = new RichTextBox(
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = true,
            BackColor = Color.FromArgb(18, 18, 18),
            ForeColor = Color.FromArgb(228, 228, 231),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 13.5f),
            DetectUrls = false)

        contentPanel.Controls.Add(contentBox) |> ignore

        let statusLabel = new Label(
            Dock = DockStyle.Bottom,
            Height = 32,
            Text = "Loading...",
            BackColor = Color.FromArgb(24, 24, 27),
            ForeColor = Color.FromArgb(161, 161, 170),
            Font = new Font("Segoe UI", 9.0f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = Padding(10, 0, 0, 0))

        form.Controls.Add(contentPanel) |> ignore
        form.Controls.Add(leftPanel) |> ignore
        form.Controls.Add(topBar) |> ignore
        form.Controls.Add(statusLabel) |> ignore

        let styleButton (btn: Button) (bg: Color) (fg: Color) =
            btn.FlatStyle <- FlatStyle.Flat
            btn.BackColor <- bg
            btn.ForeColor <- fg
            btn.FlatAppearance.BorderSize <- 0
            btn.Font <- new Font("Segoe UI", 9.5f, FontStyle.Bold)
            btn.Cursor <- Cursors.Hand
            btn.Height <- 38

        let previousButton = new Button(Text = "<", Width = 42, Location = Point(14, 13))
        styleButton previousButton (Color.FromArgb(63, 63, 70)) Color.White

        let nextButton = new Button(Text = ">", Width = 42, Location = Point(64, 13))
        styleButton nextButton (Color.FromArgb(63, 63, 70)) Color.White

        let playButton = new Button(Text = "Play", Width = 84, Location = Point(126, 13))
        styleButton playButton (Color.FromArgb(22, 101, 52)) Color.White

        let stopButton = new Button(Text = "Stop", Width = 84, Location = Point(220, 13))
        styleButton stopButton (Color.FromArgb(153, 27, 27)) Color.White

        let speedLabel = new Label(
            Text = "Speed",
            Location = Point(330, 22),
            ForeColor = Color.FromArgb(244, 244, 245),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true)

        let speedTrack = new TrackBar(
            Minimum = -10,
            Maximum = 10,
            Value = 0,
            Location = Point(388, 12),
            Width = 160,
            Height = 42,
            BackColor = Color.FromArgb(24, 24, 27))

        let speedValueLabel = new Label(
            Text = "0",
            Location = Point(555, 22),
            ForeColor = Color.FromArgb(161, 161, 170),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true)

        let voiceLabel = new Label(
            Text = "Voice",
            Location = Point(600, 22),
            ForeColor = Color.FromArgb(244, 244, 245),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true)

        let voiceBox = new ComboBox(
            Location = Point(650, 17),
            Width = 250,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.FromArgb(244, 244, 245),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.0f))

        let engineBox = new ComboBox(
            Location = Point(912, 17),
            Width = 132,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.FromArgb(244, 244, 245),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.0f))

        engineBox.Items.Add("Windows Offline") |> ignore
        engineBox.Items.Add("Offline Neural") |> ignore
        engineBox.Items.Add("Azure Neural") |> ignore
        engineBox.SelectedIndex <- 0

        let testVoiceButton = new Button(Text = "Test", Width = 58, Location = Point(1052, 13))
        styleButton testVoiceButton (Color.FromArgb(63, 63, 70)) Color.White

        speedTrack.Scroll.Add(fun _ ->
            speedValueLabel.Text <- if speedTrack.Value > 0 then sprintf "+%d" speedTrack.Value else string speedTrack.Value)

        topBar.Controls.Add(previousButton) |> ignore
        topBar.Controls.Add(nextButton) |> ignore
        topBar.Controls.Add(playButton) |> ignore
        topBar.Controls.Add(stopButton) |> ignore
        topBar.Controls.Add(speedLabel) |> ignore
        topBar.Controls.Add(speedTrack) |> ignore
        topBar.Controls.Add(speedValueLabel) |> ignore
        topBar.Controls.Add(voiceLabel) |> ignore
        topBar.Controls.Add(voiceBox) |> ignore
        topBar.Controls.Add(engineBox) |> ignore
        topBar.Controls.Add(testVoiceButton) |> ignore

        let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
        let allBooks = loadBooks root
        writeLog (sprintf "Loaded %d books from USFM." allBooks.Length)

        let mutable currentSelection: (Book * Chapter) option = None
        let mutable speechSynthesizer: SpeechSynthesizer option = None
        let mutable azureSpeechSynthesizer: Microsoft.CognitiveServices.Speech.SpeechSynthesizer option = None
        let mutable kokoroTts: KokoroTTS option = None

        let selectedEngine () =
            if engineBox.SelectedItem = null then "Windows Offline" else string engineBox.SelectedItem

        let loadVoices () =
            try
                voiceBox.Items.Clear()
                if selectedEngine() = "Azure Neural" then
                    for voice in azureNeuralVoices do
                        voiceBox.Items.Add(voice) |> ignore
                    if voiceBox.Items.Count > 0 then
                        voiceBox.SelectedIndex <- 0
                    let hasAzureConfig =
                        not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OEB_AZURE_SPEECH_KEY")))
                        && not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OEB_AZURE_SPEECH_REGION")))
                    statusLabel.Text <-
                        if hasAzureConfig then "Azure neural voices ready"
                        else "Azure Neural needs OEB_AZURE_SPEECH_KEY and OEB_AZURE_SPEECH_REGION"
                elif selectedEngine() = "Offline Neural" then
                    for voice in kokoroOfflineVoices do
                        voiceBox.Items.Add(voice) |> ignore
                    if voiceBox.Items.Count > 0 then
                        voiceBox.SelectedIndex <- 0
                    statusLabel.Text <- "Offline neural voices ready. First play may download the local model."
                else
                    use synth = new SpeechSynthesizer()
                    let voices =
                        synth.GetInstalledVoices()
                        |> Seq.cast<InstalledVoice>
                        |> Seq.filter (fun voice -> voice.Enabled)
                        |> Seq.map (fun voice -> voice.VoiceInfo.Name)
                        |> Seq.toArray

                    for voice in voices do
                        voiceBox.Items.Add(voice) |> ignore

                    if voiceBox.Items.Count > 0 then
                        voiceBox.SelectedIndex <- 0
                        statusLabel.Text <- sprintf "Loaded %d Windows speech voices" voiceBox.Items.Count
                    else
                        statusLabel.Text <- "No Windows speech voices are installed or enabled"
            with ex ->
                statusLabel.Text <- sprintf "Could not load speech voices: %s" ex.Message

        let bookByName =
            allBooks
            |> List.map (fun book -> book.Name.ToLowerInvariant(), book)
            |> Map.ofList

        let availableBooksInCanonicalOrder =
            canonicalBookNames
            |> List.choose (fun name -> Map.tryFind (name.ToLowerInvariant()) bookByName)

        let showChapter (book: Book) (chapter: Chapter) =
            currentSelection <- Some(book, chapter)
            contentBox.Text <- sprintf "%s %d\n\n%s" book.Name chapter.Number chapter.DisplayText
            statusLabel.Text <- sprintf "Viewing: %s %d" book.Name chapter.Number

        let setStatus (text: string) =
            if form.IsDisposed then ()
            elif form.InvokeRequired then
                form.BeginInvoke(Action(fun () -> statusLabel.Text <- text)) |> ignore
            else
                statusLabel.Text <- text

        let populateTree (filterText: string) =
            treeView.BeginUpdate()
            treeView.Nodes.Clear()
            let query = filterText.Trim().ToLowerInvariant()

            for sectionName, sectionBooks in bibleSections do
                let sectionNode = TreeNode(sectionName)
                sectionNode.ForeColor <- Color.FromArgb(212, 212, 216)

                for canonicalName in sectionBooks do
                    match Map.tryFind (canonicalName.ToLowerInvariant()) bookByName with
                    | Some book ->
                        let chapterMatches =
                            book.Chapters
                            |> List.filter (fun chapter ->
                                String.IsNullOrEmpty(query)
                                || sectionName.ToLowerInvariant().Contains(query)
                                || book.Name.ToLowerInvariant().Contains(query)
                                || sprintf "%s %d" book.Name chapter.Number |> fun name -> name.ToLowerInvariant().Contains(query)
                                || chapter.DisplayText.ToLowerInvariant().Contains(query))

                        if not (List.isEmpty chapterMatches) then
                            let bookNode = TreeNode(sprintf "%s (%d)" book.Name book.Chapters.Length)
                            bookNode.Tag <- BookNode book

                            for chapter in chapterMatches do
                                let chapterNode = TreeNode(sprintf "Chapter %d" chapter.Number)
                                chapterNode.Tag <- ChapterNode(book, chapter)
                                bookNode.Nodes.Add(chapterNode) |> ignore

                            sectionNode.Nodes.Add(bookNode) |> ignore
                    | None ->
                        if String.IsNullOrEmpty(query) || sectionName.ToLowerInvariant().Contains(query) || canonicalName.ToLowerInvariant().Contains(query) then
                            let bookNode = TreeNode(sprintf "%s (not included)" canonicalName)
                            bookNode.Tag <- MissingBookNode canonicalName
                            bookNode.ForeColor <- Color.FromArgb(113, 113, 122)
                            sectionNode.Nodes.Add(bookNode) |> ignore

                if sectionNode.Nodes.Count > 0 then
                    treeView.Nodes.Add(sectionNode) |> ignore

            if treeView.Nodes.Count > 0 then
                treeView.Nodes.[0].Expand()

            treeView.EndUpdate()

        let selectTreeChapter (book: Book) (chapter: Chapter) =
            let mutable selected = false
            for sectionNode in treeView.Nodes do
                if not selected then
                    for bookNode in sectionNode.Nodes do
                        if not selected then
                            match bookNode.Tag with
                            | :? TreeNodeTag as tag ->
                                match tag with
                                | BookNode taggedBook when taggedBook.Name = book.Name ->
                                    sectionNode.Expand()
                                    bookNode.Expand()
                                    for chapterNode in bookNode.Nodes do
                                        if not selected then
                                            match chapterNode.Tag with
                                            | :? TreeNodeTag as chapterTag ->
                                                match chapterTag with
                                                | ChapterNode(_, taggedChapter) when taggedChapter.Number = chapter.Number ->
                                                    treeView.SelectedNode <- chapterNode
                                                    selected <- true
                                                | _ -> ()
                                            | _ -> ()
                                | _ -> ()
                            | _ -> ()

        let moveChapter offset =
            match currentSelection with
            | None -> ()
            | Some(book, chapter) ->
                let flat =
                    availableBooksInCanonicalOrder
                    |> List.collect (fun b -> b.Chapters |> List.map (fun c -> b, c))

                let currentIndex =
                    flat
                    |> List.tryFindIndex (fun (b, c) -> b.Name = book.Name && c.Number = chapter.Number)

                match currentIndex with
                | Some index ->
                    let targetIndex = index + offset
                    if targetIndex >= 0 && targetIndex < flat.Length then
                        let targetBook, targetChapter = flat.[targetIndex]
                        selectTreeChapter targetBook targetChapter
                | None -> ()

        let stopTTS updateStatus =
            match speechSynthesizer with
            | Some synth ->
                try synth.SpeakAsyncCancelAll() with _ -> ()
                synth.Dispose()
                speechSynthesizer <- None
                if updateStatus then
                    setStatus "Stopped text-to-speech"
            | None -> ()
            match azureSpeechSynthesizer with
            | Some synth ->
                try
                    synth.StopSpeakingAsync().GetAwaiter().GetResult()
                with _ -> ()
                synth.Dispose()
                azureSpeechSynthesizer <- None
                if updateStatus then
                    setStatus "Stopped neural speech"
            | None -> ()
            match kokoroTts with
            | Some tts ->
                try tts.StopPlayback() with _ -> ()
                if updateStatus then
                    setStatus "Stopped offline neural speech"
            | None -> ()

        let speakWindowsText (title: string) (text: string) =
            stopTTS false
            if String.IsNullOrWhiteSpace(text) then
                setStatus "No text available to read"
            elif voiceBox.Items.Count = 0 then
                setStatus "No Windows speech voices are installed or enabled"
            else
                try
                    let synth = new SpeechSynthesizer()
                    synth.SetOutputToDefaultAudioDevice()
                    if voiceBox.SelectedItem <> null then
                        synth.SelectVoice(string voiceBox.SelectedItem)
                    synth.Rate <- speedTrack.Value
                    synth.Volume <- 100
                    synth.SpeakStarted.Add(fun _ ->
                        setStatus (sprintf "Speaking: %s" title))
                    synth.SpeakCompleted.Add(fun _ ->
                        match speechSynthesizer with
                        | Some active when Object.ReferenceEquals(active, synth) ->
                            speechSynthesizer <- None
                            setStatus (sprintf "Finished: %s" title)
                            synth.Dispose()
                        | _ -> ())
                    speechSynthesizer <- Some synth
                    synth.SpeakAsync(text) |> ignore
                    setStatus (sprintf "Speaking: %s" title)
                with ex ->
                    setStatus (sprintf "Error starting speech: %s" ex.Message)

        let speakAzureText (title: string) (text: string) =
            stopTTS false
            let key = Environment.GetEnvironmentVariable("OEB_AZURE_SPEECH_KEY")
            let region = Environment.GetEnvironmentVariable("OEB_AZURE_SPEECH_REGION")
            if String.IsNullOrWhiteSpace(text) then
                setStatus "No text available to read"
            elif String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(region) then
                setStatus "Azure Neural needs OEB_AZURE_SPEECH_KEY and OEB_AZURE_SPEECH_REGION"
            else
                let voiceName =
                    if voiceBox.SelectedItem = null then "en-US-JennyNeural" else string voiceBox.SelectedItem
                setStatus (sprintf "Connecting Azure Neural: %s" voiceName)
                Task.Run(fun () ->
                    try
                        let config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region)
                        config.SpeechSynthesisVoiceName <- voiceName
                        let synth = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config)
                        azureSpeechSynthesizer <- Some synth
                        setStatus (sprintf "Speaking: %s" title)
                        let result = synth.SpeakTextAsync(text).GetAwaiter().GetResult()
                        azureSpeechSynthesizer <- None
                        synth.Dispose()
                        if result.Reason = Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted then
                            setStatus (sprintf "Finished: %s" title)
                        else
                            setStatus (sprintf "Azure speech stopped: %A" result.Reason)
                    with ex ->
                        azureSpeechSynthesizer <- None
                        setStatus (sprintf "Azure speech error: %s" ex.Message)) |> ignore

        let kokoroSpeed () =
            0.65f + (single (speedTrack.Value + 10) / 20.0f) * 0.85f

        let speakKokoroText (title: string) (text: string) =
            stopTTS false
            if String.IsNullOrWhiteSpace(text) then
                setStatus "No text available to read"
            else
                let voiceName =
                    if voiceBox.SelectedItem = null then "af_heart" else string voiceBox.SelectedItem
                Task.Run(fun () ->
                    try
                        let tts =
                            match kokoroTts with
                            | Some existing -> existing
                            | None ->
                                setStatus "Loading offline neural model..."
                                let loaded = KokoroTTS.LoadModel(KModel.int8, null)
                                kokoroTts <- Some loaded
                                loaded

                        let voice = KokoroVoiceManager.GetVoice(voiceName)
                        let config = KokoroTTSPipelineConfig()
                        config.Speed <- kokoroSpeed()
                        config.PreprocessText <- true
                        tts.SetVolume(1.0f)
                        setStatus (sprintf "Speaking offline neural: %s" title)
                        tts.SpeakFast(text, voice, config) |> ignore
                    with ex ->
                        setStatus (sprintf "Offline neural speech error: %s" ex.Message)) |> ignore

        let speakText (title: string) (text: string) =
            if selectedEngine() = "Azure Neural" then
                speakAzureText title text
            elif selectedEngine() = "Offline Neural" then
                speakKokoroText title text
            else
                speakWindowsText title text

        let playTTS () =
            match currentSelection with
            | None -> setStatus "Please select a chapter to play"
            | Some(book, chapter) -> speakText (sprintf "%s %d" book.Name chapter.Number) chapter.SpeechText

        speedTrack.Scroll.Add(fun _ ->
            match speechSynthesizer with
            | Some synth ->
                try
                    synth.Rate <- speedTrack.Value
                    setStatus (sprintf "Speaking speed set to %d" speedTrack.Value)
                with ex ->
                    setStatus (sprintf "Could not update speech speed: %s" ex.Message)
            | None -> ())

        engineBox.SelectedIndexChanged.Add(fun _ -> loadVoices())
        searchBox.TextChanged.Add(fun _ -> populateTree searchBox.Text)
        previousButton.Click.Add(fun _ -> moveChapter -1)
        nextButton.Click.Add(fun _ -> moveChapter 1)
        playButton.Click.Add(fun _ -> playTTS())
        stopButton.Click.Add(fun _ -> stopTTS true)
        testVoiceButton.Click.Add(fun _ -> speakText "voice test" "Open English Bible voice test.")

        treeView.AfterSelect.Add(fun e ->
            match e.Node with
            | null -> ()
            | node ->
                match node.Tag with
                | :? TreeNodeTag as tag ->
                    match tag with
                    | BookNode book ->
                        match book.Chapters with
                        | firstChapter :: _ -> showChapter book firstChapter
                        | [] ->
                            currentSelection <- None
                            contentBox.Text <- "No chapters found."
                            statusLabel.Text <- sprintf "Viewing: %s" book.Name
                    | ChapterNode(book, chapter) ->
                        showChapter book chapter
                    | MissingBookNode bookName ->
                        currentSelection <- None
                        contentBox.Text <- sprintf "%s\n\nThis book is not included in the Open English Bible artifacts currently available in this repository." bookName
                        statusLabel.Text <- sprintf "%s is not included in this OEB release" bookName
                | _ -> ())

        form.FormClosing.Add(fun _ -> stopTTS false)

        if List.isEmpty allBooks then
            contentBox.Text <- "Bible content not found. Please ensure the OEB USFM artifacts are available."
            statusLabel.Text <- "Error: Bible content not found"
        else
            loadVoices()
            populateTree ""
            statusLabel.Text <- sprintf "Loaded %d available books successfully" availableBooksInCanonicalOrder.Length
            if treeView.Nodes.Count > 0 && treeView.Nodes.[0].Nodes.Count > 0 then
                treeView.Nodes.[0].Expand()
                let firstBookNode = treeView.Nodes.[0].Nodes.[0]
                treeView.SelectedNode <- firstBookNode
                firstBookNode.Collapse()

        Application.Run(form)
        0
    with ex ->
        writeLog (sprintf "Critical startup exception: %s\n%s" ex.Message ex.StackTrace)
        1
