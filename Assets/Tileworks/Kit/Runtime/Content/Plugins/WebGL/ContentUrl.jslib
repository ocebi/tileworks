mergeInto(LibraryManager.library, {
  // The page sets window.tileworksContentUrl (an absolute URL) before the engine starts; see the WebGL template.
  TileworksContentUrl: function () {
    var url = (typeof window !== "undefined" && window.tileworksContentUrl) ? String(window.tileworksContentUrl) : "";
    var size = lengthBytesUTF8(url) + 1;
    var buffer = _malloc(size);
    stringToUTF8(url, buffer, size);
    return buffer;
  }
});
