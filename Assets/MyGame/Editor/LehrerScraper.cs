using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Lehrerplan.EditorTools
{
    /// <summary>
    /// Holt per Regex die Lehrerdaten (Name, Raum, Tel-DW, Sprechstunde) von
    /// https://www.htl-salzburg.ac.at/lehrerinnen.html und speichert sie als JSON
    /// unter Assets/MyGame/Resources/lehrer.json.
    /// </summary>
    public static class LehrerScraper
    {
        const string BasisUrl = "https://www.htl-salzburg.ac.at";
        const string ListenUrl = BasisUrl + "/lehrerinnen.html";
        const string ZielDatei = "Assets/MyGame/Resources/lehrer.json";

        // Liste: <a class="value" href="/lehrerinnen-details/....html"><span class="text">Name</span></a>
        static readonly Regex DetailLinkRegex = new Regex(
            @"<a\s+class=""value""\s+href=""(?<href>/lehrerinnen-details/[^""]+\.html)"">",
            RegexOptions.Compiled);

        // Detailseite: <div class="field Raum"> <div class="label">..</div> <div class="value"> <span class="text">B 304</span>
        static Regex FeldRegex(string feld) => new Regex(
            @"<div class=""field " + feld + @""">\s*<div class=""label"">[^<]*</div>\s*" +
            @"<div class=""value"">\s*<span class=""text"">(?<wert>[^<]*)</span>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        static readonly Regex NameRegex = FeldRegex("Lehrername");
        static readonly Regex RaumRegex = FeldRegex("Raum");
        static readonly Regex TelefonRegex = FeldRegex("Telefon");
        static readonly Regex SprechstundeRegex = FeldRegex("SprStunde");

        // Fußzeile: <p>Sekretariat: +43 662 453610 - 0</p>
        static readonly Regex ZentraleRegex = new Regex(
            @"Sekretariat:\s*(?<nr>\+[\d ]+?)\s*-\s*0", RegexOptions.Compiled);

        // "B 304", "b304", "B-304" -> "B304"
        static readonly Regex RaumNormRegex = new Regex(
            @"^\s*(?<trakt>[A-Za-z])\s*[- ]?\s*(?<nr>\d+[a-zA-Z]?)\s*$", RegexOptions.Compiled);

        [MenuItem("Lehrerplan/1 - Lehrerdaten von HTL-Homepage laden")]
        public static async void LadenMenu()
        {
            try
            {
                var sammlung = await LadeAlleAsync();
                Speichern(sammlung);
                EditorUtility.DisplayDialog("Lehrerplan",
                    $"{sammlung.lehrer.Count} Lehrer gespeichert in\n{ZielDatei}", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static async Task<LehrerSammlung> LadeAlleAsync()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Lehrerplan-Schulprojekt/1.0");

            string liste = await http.GetStringAsync(ListenUrl);

            var links = DetailLinkRegex.Matches(liste).Cast<Match>()
                .Select(m => BasisUrl + m.Groups["href"].Value)
                .Distinct()
                .ToList();
            Debug.Log($"[LehrerScraper] {links.Count} Detailseiten gefunden.");

            var sammlung = new LehrerSammlung
            {
                quelle = ListenUrl,
                abgerufenAm = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                telefonZentrale = ZentraleMatch(liste),
            };

            // Höflich: max. 4 gleichzeitige Anfragen
            var drossel = new System.Threading.SemaphoreSlim(4);
            int fertig = 0;
            var tasks = links.Select(async url =>
            {
                await drossel.WaitAsync();
                try
                {
                    string html = await http.GetStringAsync(url);
                    return ParseDetail(html, url);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LehrerScraper] Fehler bei {url}: {e.Message}");
                    return null;
                }
                finally
                {
                    drossel.Release();
                    fertig++;
                }
            }).ToList();

            while (!tasks.All(t => t.IsCompleted))
            {
                EditorUtility.DisplayProgressBar("Lehrerplan", $"Lade Lehrerdaten ... {fertig}/{links.Count}",
                    links.Count == 0 ? 1f : (float)fertig / links.Count);
                await Task.Delay(100);
            }

            sammlung.lehrer = tasks.Select(t => t.Result)
                .Where(l => l != null)
                .OrderBy(l => l.raum).ThenBy(l => l.name)
                .ToList();
            return sammlung;
        }

        public static Lehrer ParseDetail(string html, string url)
        {
            string name = Wert(NameRegex, html);
            if (string.IsNullOrEmpty(name)) return null;

            return new Lehrer
            {
                name = name,
                raum = NormalisiereRaum(Wert(RaumRegex, html)),
                telefonDw = Wert(TelefonRegex, html),
                sprechstunde = Wert(SprechstundeRegex, html),
                url = url,
            };
        }

        public static string NormalisiereRaum(string raum)
        {
            if (string.IsNullOrWhiteSpace(raum)) return "";
            var m = RaumNormRegex.Match(raum);
            return m.Success
                ? m.Groups["trakt"].Value.ToUpperInvariant() + m.Groups["nr"].Value.ToLowerInvariant()
                : raum.Trim();
        }

        static string Wert(Regex regex, string html)
        {
            var m = regex.Match(html);
            return m.Success ? WebUtility.HtmlDecode(m.Groups["wert"].Value).Trim() : "";
        }

        static string ZentraleMatch(string html)
        {
            var m = ZentraleRegex.Match(html);
            return m.Success ? m.Groups["nr"].Value.Trim() : "+43 662 453610";
        }

        static void Speichern(LehrerSammlung sammlung)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ZielDatei));
            File.WriteAllText(ZielDatei, JsonUtility.ToJson(sammlung, true));
            AssetDatabase.ImportAsset(ZielDatei);
            Debug.Log($"[LehrerScraper] {sammlung.lehrer.Count} Lehrer -> {ZielDatei}");
        }
    }
}
