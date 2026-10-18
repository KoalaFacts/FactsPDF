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

export async function loadStyleResources(inputUrl, fontUrlsByRole) {
  const roleNames = ['normal', 'bold', 'italic', 'boldItalic'];
  const invalid = () => Object.assign(new Error('Supply explicit font URL chains for normal, bold, italic and boldItalic (at most eight total).'),
    { code: 'B2_INPUT_FONTS' });
  if (!fontUrlsByRole || typeof fontUrlsByRole !== 'object' || Array.isArray(fontUrlsByRole)
    || Object.keys(fontUrlsByRole).some(key => !roleNames.includes(key))) throw invalid();
  const entries = [];
  for (const [role, name] of roleNames.entries()) {
    const urls = Object.hasOwn(fontUrlsByRole, name) ? fontUrlsByRole[name] : [];
    if (!Array.isArray(urls) || urls.some(url => typeof url !== 'string' || !url.trim())) throw invalid();
    if (urls.length > 8 - entries.length) throw invalid();
    entries.push(...urls.map(url => ({ role, url })));
  }
  // Reuse strict UTF-8 and explicit-resource errors from the original entry.
  const { html } = await loadResources(inputUrl, []);
  const fonts = [];
  let total = 0;
  for (const entry of entries) {
    const bytes = await read(entry.url);
    if (!bytes.length)
      throw Object.assign(new Error(`Explicit font is empty: ${entry.url}`), { code: 'B2_RESOURCE_EMPTY' });
    total += bytes.length;
    if (bytes.length > 33_554_432 || total > 67_108_864)
      throw Object.assign(new Error('Explicit font resources exceed the probe transport byte limits.'), { code: 'B2_RESOURCE_LIMIT' });
    fonts.push(bytes);
  }
  const fontBytes = new Uint8Array(total);
  let offset = 0;
  for (const font of fonts) { fontBytes.set(font, offset); offset += font.length; }
  return { html, fontBytes, fontLengths: Int32Array.from(fonts, bytes => bytes.length),
    fontRoles: Int32Array.from(entries, entry => entry.role) };
}

// Private explicit-byte transport. Caller-owned arrays and interop copies are
// outside the loader's budget; reject their shape/caps before our packed copy.
export function packImageResources(images) {
  const invalid = () => Object.assign(new Error('Supply at most 16 uniquely named nonempty Uint8Array images.'), { code: 'B2_INPUT_IMAGES' });
  if (!Array.isArray(images) || images.length > 16) throw invalid();
  const names = new Set(); let total = 0;
  for (const image of images) {
    if (!image || typeof image.name !== 'string' || image.name.length > 128
      || !/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(image.name) || names.has(image.name)
      || !(image.bytes instanceof Uint8Array) || image.bytes.length === 0) throw invalid();
    names.add(image.name);
    if (image.bytes.length > 8_388_608 || image.bytes.length > 33_554_432 - total)
      throw Object.assign(new Error('Explicit image resources exceed transport byte limits.'), { code: 'B2_RESOURCE_LIMIT' });
    total += image.bytes.length;
  }
  const imageBytes = new Uint8Array(total); let offset = 0;
  for (const image of images) { imageBytes.set(image.bytes, offset); offset += image.bytes.length; }
  return { imageBytes, imageLengths: Int32Array.from(images, image => image.bytes.length), imageNames: [...names] };
}

async function read(url) {
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok)
    throw Object.assign(new Error(`Explicit resource HTTP ${response.status}: ${url}`), { code: 'B2_RESOURCE_HTTP' });
  return new Uint8Array(await response.arrayBuffer());
}
