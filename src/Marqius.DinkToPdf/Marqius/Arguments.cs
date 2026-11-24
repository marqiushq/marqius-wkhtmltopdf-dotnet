using System;
using System.Collections.Generic;
using System.Globalization;

namespace DinkToPdf.Marqius
{
    /// <summary>
    /// One set of libwkhtmltox settings (global or for one object), as DinkToPdf and other callers set
    /// them by name: plain values, and lists such as load.customHeaders, which are set with
    /// "name.append" and then "name[i]" = "key\nvalue".
    /// </summary>
    internal sealed class SettingBag
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly bool global;

        internal SettingBag(bool global)
        {
            this.global = global;
        }

        internal bool Set(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.EndsWith(".append", StringComparison.Ordinal))
            {
                var list = name.Substring(0, name.Length - ".append".Length);
                if (!Arguments.IsList(list, global))
                    return false;
                List(list).Add(null);
                return true;
            }
            var open = name.IndexOf('[');
            if (open > 0 && name.EndsWith("]", StringComparison.Ordinal))
            {
                var list = name.Substring(0, open);
                if (!Arguments.IsList(list, global)
                    || !int.TryParse(name.Substring(open + 1, name.Length - open - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                    return false;
                var items = List(list);
                while (items.Count <= index)
                    items.Add(null);
                items[index] = value;
                return true;
            }
            if (!Arguments.Knows(name, global))
                return false;
            values[name] = value;
            return true;
        }

        internal string Get(string name)
        {
            return values.TryGetValue(name, out var v) ? v : "";
        }

        internal bool Has(string name, out string value)
        {
            return values.TryGetValue(name, out value) && value != null;
        }

        internal IEnumerable<KeyValuePair<string, string>> Values => values;

        internal IReadOnlyList<string> Items(string list)
        {
            return lists.TryGetValue(list, out var items) ? items : (IReadOnlyList<string>)Array.Empty<string>();
        }

        private List<string> List(string name)
        {
            if (!lists.TryGetValue(name, out var items))
                lists[name] = items = new List<string>();
            return items;
        }
    }

    /// <summary>
    /// libwkhtmltox's setting names, as wkhtmltopdf's command line spells each one.
    /// </summary>
    internal static class Arguments
    {
        // global settings that take a value: name -> option
        private static readonly Dictionary<string, string> GlobalValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["size.paperSize"] = "--page-size",
            ["size.width"] = "--page-width",
            ["size.height"] = "--page-height",
            ["orientation"] = "--orientation",
            ["dpi"] = "--dpi",
            ["pageOffset"] = "--page-offset",
            ["copies"] = "--copies",
            ["outlineDepth"] = "--outline-depth",
            ["dumpOutline"] = "--dump-outline",
            ["documentTitle"] = "--title",
            ["margin.top"] = "--margin-top",
            ["margin.bottom"] = "--margin-bottom",
            ["margin.left"] = "--margin-left",
            ["margin.right"] = "--margin-right",
            ["imageDPI"] = "--image-dpi",
            ["imageQuality"] = "--image-quality",
            ["load.cookieJar"] = "--cookie-jar",
            ["viewportSize"] = "--viewport-size",
        };

        // global switches: name -> (option when true, option when false)
        private static readonly Dictionary<string, (string, string)> GlobalSwitches = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["collate"] = ("--collate", "--no-collate"),
            ["outline"] = ("--outline", "--no-outline"),
            ["useCompression"] = (null, "--no-pdf-compression"),
        };

        // global settings with no option of their own: read where they apply, or not needed
        private static readonly HashSet<string> GlobalOthers = new HashSet<string>(StringComparer.Ordinal)
        {
            "colorMode", "out", "resolution", "useGraphics",
        };

        // object settings that take a value
        private static readonly Dictionary<string, string> ObjectValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["web.minimumFontSize"] = "--minimum-font-size",
            ["web.defaultEncoding"] = "--encoding",
            ["web.userStyleSheet"] = "--user-style-sheet",
            ["header.fontSize"] = "--header-font-size",
            ["header.fontName"] = "--header-font-name",
            ["header.left"] = "--header-left",
            ["header.center"] = "--header-center",
            ["header.right"] = "--header-right",
            ["header.spacing"] = "--header-spacing",
            ["header.htmlUrl"] = "--header-html",
            ["footer.fontSize"] = "--footer-font-size",
            ["footer.fontName"] = "--footer-font-name",
            ["footer.left"] = "--footer-left",
            ["footer.center"] = "--footer-center",
            ["footer.right"] = "--footer-right",
            ["footer.spacing"] = "--footer-spacing",
            ["footer.htmlUrl"] = "--footer-html",
            ["load.username"] = "--username",
            ["load.password"] = "--password",
            ["load.jsdelay"] = "--javascript-delay",
            ["load.windowStatus"] = "--window-status",
            ["load.zoomFactor"] = "--zoom",
            ["load.loadErrorHandling"] = "--load-error-handling",
            ["load.proxy"] = "--proxy",
            ["load.cacheDir"] = "--cache-dir",
            ["toc.captionText"] = "--toc-header-text",
            ["toc.indentation"] = "--toc-level-indentation",
            ["toc.fontScale"] = "--toc-text-size-shrink",
            ["tocXsl"] = "--xsl-style-sheet",
        };

        // object switches: name -> (option when true, option when false)
        private static readonly Dictionary<string, (string, string)> ObjectSwitches = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["useExternalLinks"] = ("--enable-external-links", "--disable-external-links"),
            ["useLocalLinks"] = ("--enable-internal-links", "--disable-internal-links"),
            ["produceForms"] = ("--enable-forms", "--disable-forms"),
            ["includeInOutline"] = ("--include-in-outline", "--exclude-from-outline"),
            ["web.background"] = ("--background", "--no-background"),
            ["web.loadImages"] = ("--images", "--no-images"),
            ["web.enableJavascript"] = ("--enable-javascript", "--disable-javascript"),
            ["web.enableIntelligentShrinking"] = ("--enable-smart-shrinking", "--disable-smart-shrinking"),
            ["web.printMediaType"] = ("--print-media-type", "--no-print-media-type"),
            ["web.enablePlugins"] = ("--enable-plugins", "--disable-plugins"),
            ["header.line"] = ("--header-line", "--no-header-line"),
            ["footer.line"] = ("--footer-line", "--no-footer-line"),
            ["load.blockLocalFileAccess"] = ("--disable-local-file-access", "--enable-local-file-access"),
            ["load.stopSlowScript"] = ("--stop-slow-scripts", "--no-stop-slow-scripts"),
            ["load.debugJavascript"] = ("--debug-javascript", "--no-debug-javascript"),
            ["load.repeatCustomHeaders"] = ("--custom-header-propagation", "--no-custom-header-propagation"),
            ["toc.useDottedLines"] = (null, "--disable-dotted-lines"),
            ["toc.forwardLinks"] = (null, "--disable-toc-links"),
            ["toc.backLinks"] = ("--enable-toc-back-links", "--disable-toc-back-links"),
        };

        // object settings with no option of their own: "page" is the input; wkhtmltopdf counts pages by itself
        private static readonly HashSet<string> ObjectOthers = new HashSet<string>(StringComparer.Ordinal)
        {
            "page", "pagesCount", "isTableOfContent",
        };

        // lists of "key\nvalue" pairs: name -> option taking key and value
        private static readonly Dictionary<string, string> ObjectLists = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["load.customHeaders"] = "--custom-header",
            ["load.cookies"] = "--cookie",
            ["load.post"] = "--post",
            ["replacements"] = "--replace",
        };

        internal static bool Knows(string name, bool global)
        {
            return global
                ? GlobalValues.ContainsKey(name) || GlobalSwitches.ContainsKey(name) || GlobalOthers.Contains(name)
                : ObjectValues.ContainsKey(name) || ObjectSwitches.ContainsKey(name) || ObjectOthers.Contains(name);
        }

        internal static bool IsList(string name, bool global)
        {
            return !global && ObjectLists.ContainsKey(name);
        }

        /// <summary>The command line's global options for these settings.</summary>
        internal static void Global(SettingBag bag, List<string> args)
        {
            foreach (var pair in bag.Values)
            {
                var value = pair.Value;
                if (value == null)
                    continue;
                if (GlobalValues.TryGetValue(pair.Key, out var option))
                {
                    if (value.Length > 0)
                    {
                        args.Add(option);
                        args.Add(pair.Key == "orientation" ? Capitalised(value) : value);
                    }
                }
                else if (GlobalSwitches.TryGetValue(pair.Key, out var both))
                {
                    var chosen = IsTrue(value) ? both.Item1 : both.Item2;
                    if (chosen != null)
                        args.Add(chosen);
                }
                else if (pair.Key == "colorMode" && value.Equals("grayscale", StringComparison.OrdinalIgnoreCase))
                {
                    args.Add("--grayscale");
                }
            }
        }

        /// <summary>The command line's page options for one object.</summary>
        internal static void Object(SettingBag bag, List<string> args)
        {
            foreach (var pair in bag.Values)
            {
                var value = pair.Value;
                if (value == null)
                    continue;
                if (ObjectValues.TryGetValue(pair.Key, out var option))
                {
                    if (value.Length > 0)
                    {
                        args.Add(option);
                        args.Add(pair.Key == "load.loadErrorHandling" ? value.ToLowerInvariant() : value);
                    }
                }
                else if (ObjectSwitches.TryGetValue(pair.Key, out var both))
                {
                    var chosen = IsTrue(value) ? both.Item1 : both.Item2;
                    if (chosen != null)
                        args.Add(chosen);
                }
            }
            foreach (var list in ObjectLists)
            {
                foreach (var item in bag.Items(list.Key))
                {
                    if (item == null)
                        continue;
                    var cut = item.IndexOf('\n');
                    args.Add(list.Value);
                    args.Add(cut < 0 ? item : item.Substring(0, cut));
                    args.Add(cut < 0 ? "" : item.Substring(cut + 1));
                }
            }
        }

        internal static bool IsTrue(string value)
        {
            return value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
        }

        private static string Capitalised(string value)
        {
            return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
        }
    }
}
