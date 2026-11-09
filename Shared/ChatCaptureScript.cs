namespace GTAWLogParser.Shared
{
    /// <summary>
    /// The JavaScript evaluated inside GTAW's FiveM HUD to read the visible chat.
    /// Shared by the Assistant and the Mini parser so both capture identically.
    /// </summary>
    internal static class ChatCaptureScript
    {
        /// <summary>
        /// Returns a JSON array with one string per visible chat line.
        ///
        /// The HUD consumes the formatting markers players type: *action* is
        /// rendered as an emote-coloured run and /emphasis/ as italics, so the
        /// rendered text (innerText) no longer contains the asterisks or slashes.
        /// Instead of reading innerText, this walks each line's text nodes and
        /// puts the markers back around runs that are styled that way.
        ///
        /// Keep this free of double quotes: it lives in a C# verbatim string.
        /// </summary>
        public const string Expression = @"JSON.stringify((() => {
    const TIME = /\b\d{1,2}:\d{2}:\d{2}\b/;
    const TIME_ONLY = /^\[?\d{1,2}:\d{2}:\d{2}\]?$/;
    const ACTION_HINT = /(^|[\s_-])(me|emote|action|roleplay)([\s_-]|$)/i;

    const isPurple = colour => {
        const m = String(colour).match(/rgba?\(\s*(\d+)[,\s]+(\d+)[,\s]+(\d+)/);
        if (!m) return false;
        const r = m[1] / 255, g = m[2] / 255, b = m[3] / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
        if (d < 0.08) return false;
        const l = (max + min) / 2;
        const s = d / (1 - Math.abs(2 * l - 1) || 1);
        let h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
        h = (h * 60 + 360) % 360;
        return s >= 0.15 && h >= 245 && h <= 325;
    };

    const isItalic = style => style.fontStyle === 'italic' || style.fontStyle.indexOf('oblique') === 0;

    const collect = (node, hinted, out) => {
        for (const child of Array.from(node.childNodes)) {
            if (child.nodeType === 3) {
                if (!child.nodeValue) continue;
                const style = getComputedStyle(node);
                if (style.visibility === 'hidden') continue;
                out.push({ text: child.nodeValue, colour: style.color, italic: isItalic(style), hinted: hinted });
            } else if (child.nodeType === 1) {
                const style = getComputedStyle(child);
                if (style.display === 'none') continue;
                if (child.tagName === 'BR') { out.push({ text: ' ', gap: true }); continue; }
                const block = style.display.indexOf('inline') !== 0 && style.display !== 'contents';
                if (block) out.push({ text: ' ', gap: true });
                collect(child, hinted || ACTION_HINT.test(String(child.getAttribute('class') || '')), out);
                if (block) out.push({ text: ' ', gap: true });
            }
        }
    };

    const wrap = (text, open, close) => {
        const m = text.match(/^(\s*)([\s\S]*?)(\s*)$/);
        const core = m[2];
        if (!core) return text;
        if (core.length > 1 && core.charAt(0) === open.charAt(0) && core.charAt(core.length - 1) === open.charAt(0)) return text;
        return m[1] + open + core + close + m[3];
    };

    const readText = el => {
        const parts = [];
        collect(el, false, parts);

        const baseItalic = isItalic(getComputedStyle(el));
        const first = parts.find(p => !p.gap && p.text.trim() && !TIME_ONLY.test(p.text.trim()));
        const baseColour = first ? first.colour : '';
        const baseHinted = first ? first.hinted : false;

        const runs = [];
        for (const part of parts) {
            const blank = part.gap || !part.text.trim();
            const last = runs[runs.length - 1];
            if (blank) {
                if (last) last.text += part.text; else runs.push({ text: part.text, italic: false, action: false, blank: true });
                continue;
            }
            const italic = part.italic && !baseItalic;
            const action = !TIME_ONLY.test(part.text.trim()) && part.colour !== baseColour && (isPurple(part.colour) || (part.hinted && !baseHinted));
            if (last && (last.blank || (last.italic === italic && last.action === action))) {
                last.text += part.text;
                last.italic = italic;
                last.action = action;
                last.blank = false;
            } else {
                runs.push({ text: part.text, italic: italic, action: action, blank: false });
            }
        }

        let line = '';
        for (let i = 0; i < runs.length;) {
            const action = runs[i].action;
            let group = '';
            for (; i < runs.length && runs[i].action === action; i++)
                group += runs[i].italic ? wrap(runs[i].text, '/', '/') : runs[i].text;
            line += action ? wrap(group, '*', '*') : group;
        }
        return line;
    };

    const readTimestamp = el => {
        const nodes = [el].concat(Array.from(el.querySelectorAll('*')));
        for (const node of nodes) {
            for (const attribute of Array.from(node.attributes || [])) {
                const match = String(attribute.value).match(TIME);
                if (match) return match[0];
            }
            const match = String(getComputedStyle(node, '::before').content || '').match(TIME);
            if (match) return match[0];
        }
        return '';
    };

    return Array.from(document.querySelectorAll('.chat__messages > li'), el => {
        let text;
        try { text = readText(el); } catch (error) { text = el.innerText || ''; }
        text = text.replace(/\s+/g, ' ').trim();
        if (!text) return '';
        const timestamp = readTimestamp(el);
        return (timestamp ? '[' + timestamp + '] ' : '') + text;
    }).filter(Boolean);
})())";
    }
}
