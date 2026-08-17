using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Globalization;

namespace Assistant.Controllers
{
    /// <summary>
    /// Captures the visible GTAW chat from FiveM's local NUI DevTools endpoint.
    /// This is a localhost-only, read-only connection while FiveM is running.
    /// </summary>
    public static class FiveMChatCaptureController
    {
        private const string DevToolsTargetsUrl = "http://127.0.0.1:13172/json";
        private const string RootUiUrl = "nui://game/ui/root.html";
        private const string ClientFrameUrl = "https://cfx-nui-client/web/index.html";
        private const int PollIntervalMilliseconds = 500;

        private static readonly object SyncRoot = new object();
        private static readonly string SessionDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GTAW-Log-Parser-FiveM");
        private static readonly string SessionFile = Path.Combine(SessionDirectory, "current-session.txt");
        private static readonly NuiChatReader Reader = new NuiChatReader();

        private static Thread captureThread;
        private static bool runCapture;
        private static bool wasFiveMRunning;
        private static DateTime sessionStartedAt;
        private static List<string> previousVisibleLines = new List<string>();
        private static readonly Regex TimestampPrefix = new Regex(@"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]\s+");

        public static string SessionFilePath { get { return SessionFile; } }
        public static DateTime SessionStartedAt { get { return sessionStartedAt == DateTime.MinValue ? DateTime.Now : sessionStartedAt; } }

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (captureThread != null && captureThread.IsAlive)
                    return;

                Directory.CreateDirectory(SessionDirectory);
                runCapture = true;
                captureThread = new Thread(CaptureWorker) { IsBackground = true, Name = "FiveM chat capture" };
                captureThread.Start();
            }
        }

        public static void Stop()
        {
            runCapture = false;
            lock (SyncRoot)
            {
                Reader.Close();
            }
        }

        public static string ReadCapturedChat(bool removeTimestamps)
        {
            try
            {
                string chat;
                lock (SyncRoot)
                {
                    if (!File.Exists(SessionFile))
                        return string.Empty;

                    using (FileStream stream = new FileStream(SessionFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(stream))
                        chat = reader.ReadToEnd();
                }

                if (removeTimestamps)
                    chat = System.Text.RegularExpressions.Regex.Replace(chat, @"\[\d{1,2}:\d{1,2}:\d{1,2}\] ", string.Empty);

                return chat;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void CaptureWorker()
        {
            while (runCapture)
            {
                try
                {
                    bool fiveMRunning = AppController.IsFiveMRunning();
                    if (!fiveMRunning)
                    {
                        if (wasFiveMRunning)
                        {
                            lock (SyncRoot)
                            {
                                Reader.Close();
                                previousVisibleLines.Clear();
                            }
                        }

                        wasFiveMRunning = false;
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (!wasFiveMRunning)
                    {
                        lock (SyncRoot)
                        {
                            sessionStartedAt = DateTime.MinValue;
                            previousVisibleLines.Clear();
                            File.WriteAllText(SessionFile, string.Empty, new UTF8Encoding(false));
                        }
                        wasFiveMRunning = true;
                    }

                    lock (SyncRoot)
                    {
                        AppendNewLines(Reader.GetChatLines());
                    }
                }
                catch
                {
                    lock (SyncRoot)
                    {
                        Reader.Close();
                    }
                    // The HUD can reload while a server is connecting. Keep trying quietly.
                }

                Thread.Sleep(PollIntervalMilliseconds);
            }
        }

        private static void AppendNewLines(IList<string> visibleLines)
        {
            List<string> current = visibleLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList();
            if (current.Count == 0)
                return;

            int overlap = FindOverlap(previousVisibleLines, current);
            List<string> newLines = current.Skip(overlap).ToList();
            if (newLines.Count == 0)
            {
                previousVisibleLines = current;
                return;
            }

            DateTime capturedAt = DateTime.Now;
            DateTime sessionTimestamp = GetTimestamp(newLines[0], capturedAt);
            bool startOfSession = !File.Exists(SessionFile) || new FileInfo(SessionFile).Length == 0;
            using (FileStream stream = new FileStream(SessionFile, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                if (startOfSession)
                {
                    sessionStartedAt = sessionTimestamp;
                    writer.WriteLine(CreateSessionHeader(sessionTimestamp));
                }

                foreach (string line in newLines)
                    writer.WriteLine(AddTimestamp(line, capturedAt));
            }

            previousVisibleLines = current;
        }

        private static string CreateSessionHeader(DateTime timestamp)
        {
            string date = timestamp.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            return string.Format(CultureInfo.InvariantCulture, "[DATE: {0} | TIME: {1}]", date, timestamp.ToString("HH:mm:ss"));
        }

        private static string AddTimestamp(string line, DateTime capturedAt)
        {
            if (TimestampPrefix.IsMatch(line))
                return line;

            return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", capturedAt.ToString("HH:mm:ss"), line);
        }

        private static DateTime GetTimestamp(string line, DateTime fallback)
        {
            Match match = TimestampPrefix.Match(line);
            DateTime parsed;
            if (!match.Success || !DateTime.TryParseExact(match.Groups["time"].Value, "H:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return fallback;

            return fallback.Date.Add(parsed.TimeOfDay);
        }

        private static int FindOverlap(IList<string> oldLines, IList<string> newLines)
        {
            int max = Math.Min(oldLines.Count, newLines.Count);
            for (int length = max; length > 0; length--)
            {
                bool matches = true;
                for (int i = 0; i < length; i++)
                {
                    if (oldLines[oldLines.Count - length + i] != newLines[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return length;
            }

            return 0;
        }

        private sealed class NuiChatReader
        {
            private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            private ClientWebSocket socket;
            private int contextId;
            private int requestId;

            public List<string> GetChatLines()
            {
                EnsureConnected();
                const string expression = "JSON.stringify(Array.from(document.querySelectorAll('.chat__messages > li'), el => { const text = (el.innerText || '').replace(/\\s+/g, ' ').trim(); if (!text) return ''; const nodes = [el].concat(Array.from(el.querySelectorAll('*'))); let timestamp = ''; for (const node of nodes) { for (const attribute of Array.from(node.attributes || [])) { const match = String(attribute.value).match(/\\b\\d{1,2}:\\d{2}:\\d{2}\\b/); if (match) { timestamp = match[0]; break; } } if (!timestamp) { const match = String(getComputedStyle(node, '::before').content || '').match(/\\b\\d{1,2}:\\d{2}:\\d{2}\\b/); if (match) timestamp = match[0]; } if (timestamp) break; } return (timestamp ? '[' + timestamp + '] ' : '') + text; }).filter(Boolean))";
                IDictionary<string, object> result = Request("Runtime.evaluate", new Dictionary<string, object>
                {
                    { "expression", expression },
                    { "contextId", contextId },
                    { "returnByValue", true }
                });

                IDictionary<string, object> runtimeResult = DictionaryValue(result, "result");
                string value = runtimeResult != null && runtimeResult.ContainsKey("value") ? runtimeResult["value"] as string : "[]";
                object[] values = serializer.DeserializeObject(value ?? "[]") as object[];
                return values == null ? new List<string>() : values.OfType<string>().Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            }

            public void Close()
            {
                if (socket != null)
                {
                    try { socket.Abort(); } catch { }
                    socket.Dispose();
                }

                socket = null;
                contextId = 0;
                requestId = 0;
            }

            private void EnsureConnected()
            {
                if (socket != null && socket.State == WebSocketState.Open && contextId != 0)
                    return;

                Close();
                IDictionary<string, object> target = GetRootTarget();
                string socketUrl = target["webSocketDebuggerUrl"] as string;
                if (string.IsNullOrWhiteSpace(socketUrl))
                    throw new IOException("FiveM NUI DevTools is unavailable.");

                socket = new ClientWebSocket();
                socket.Options.Proxy = null;
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    socket.ConnectAsync(new Uri(socketUrl), timeout.Token).GetAwaiter().GetResult();

                IDictionary<string, object> tree = Request("Page.getFrameTree", new Dictionary<string, object>());
                IDictionary<string, object> clientFrame = FindClientFrame(DictionaryValue(tree, "frameTree"));
                if (clientFrame == null || !clientFrame.ContainsKey("id"))
                    throw new IOException("GTAW HUD is not ready.");

                IDictionary<string, object> world = Request("Page.createIsolatedWorld", new Dictionary<string, object>
                {
                    { "frameId", clientFrame["id"] },
                    { "worldName", "gtaw-log-parser-reader" },
                    { "grantUniveralAccess", true }
                });
                if (!world.ContainsKey("executionContextId"))
                    throw new IOException("GTAW HUD context is unavailable.");

                contextId = Convert.ToInt32(world["executionContextId"]);
            }

            private IDictionary<string, object> GetRootTarget()
            {
                string json;
                using (HttpClientHandler handler = new HttpClientHandler { UseProxy = false })
                using (HttpClient client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) })
                    json = client.GetStringAsync(DevToolsTargetsUrl).GetAwaiter().GetResult();

                object[] targets = serializer.DeserializeObject(json) as object[];
                if (targets != null)
                {
                    foreach (object item in targets)
                    {
                        IDictionary<string, object> target = item as IDictionary<string, object>;
                        if (target != null && target.ContainsKey("url") && (target["url"] as string) == RootUiUrl)
                            return target;
                    }
                }

                throw new IOException("FiveM root UI was not found.");
            }

            private IDictionary<string, object> Request(string method, IDictionary<string, object> parameters)
            {
                int id = ++requestId;
                string message = serializer.Serialize(new Dictionary<string, object>
                {
                    { "id", id },
                    { "method", method },
                    { "params", parameters }
                });
                byte[] data = Encoding.UTF8.GetBytes(message);
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                {
                    socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, timeout.Token).GetAwaiter().GetResult();
                    while (true)
                    {
                        IDictionary<string, object> response = Receive(timeout.Token);
                        if (!response.ContainsKey("id") || Convert.ToInt32(response["id"]) != id)
                            continue;
                        if (response.ContainsKey("error"))
                            throw new IOException("FiveM NUI DevTools returned an error.");
                        return DictionaryValue(response, "result") ?? new Dictionary<string, object>();
                    }
                }
            }

            private IDictionary<string, object> Receive(CancellationToken token)
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
                using (MemoryStream stream = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = socket.ReceiveAsync(buffer, token).GetAwaiter().GetResult();
                        if (result.MessageType == WebSocketMessageType.Close)
                            throw new IOException("FiveM NUI DevTools connection closed.");
                        stream.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    return serializer.DeserializeObject(Encoding.UTF8.GetString(stream.ToArray())) as IDictionary<string, object>;
                }
            }

            private static IDictionary<string, object> DictionaryValue(IDictionary<string, object> source, string key)
            {
                if (source == null || !source.ContainsKey(key))
                    return null;
                return source[key] as IDictionary<string, object>;
            }

            private static IDictionary<string, object> FindClientFrame(IDictionary<string, object> frameTree)
            {
                if (frameTree == null)
                    return null;

                IDictionary<string, object> frame = DictionaryValue(frameTree, "frame");
                if (frame != null && frame.ContainsKey("url") && (frame["url"] as string) == ClientFrameUrl)
                    return frame;

                object childrenObject;
                if (!frameTree.TryGetValue("childFrames", out childrenObject))
                    return null;

                IEnumerable children = childrenObject as IEnumerable;
                if (children == null)
                    return null;

                foreach (object child in children)
                {
                    IDictionary<string, object> found = FindClientFrame(child as IDictionary<string, object>);
                    if (found != null)
                        return found;
                }

                return null;
            }
        }
    }
}
