export async function loadResources(inputUrl, fontUrls) {
  if (!Array.isArray(fontUrls) || fontUrls.length > 2)
    throw Object.assign(new Error('This probe accepts at most two ordered font URLs.'), { code: 'B2_FONT_COUNT' });
  const input = await read(inputUrl);
  let html;
  try { html = new TextDecoder('utf-8', { fatal: true }).decode(input); }
  catch { throw Object.assign(new Error('HTML must be valid UTF-8.'), { code: 'B2_INPUT_UTF8' }); }
  const fonts = [];
  for (const url of fontUrls) {
    const bytes = await read(url);
    if (bytes.length === 0)
      throw Object.assign(new Error(`Explicit font is empty: ${url}`), { code: 'B2_RESOURCE_EMPTY' });
    fonts.push(bytes);
  }
  return { html, fonts };
}

async function read(url) {
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok)
    throw Object.assign(new Error(`Explicit resource HTTP ${response.status}: ${url}`), { code: 'B2_RESOURCE_HTTP' });
  return new Uint8Array(await response.arrayBuffer());
}
