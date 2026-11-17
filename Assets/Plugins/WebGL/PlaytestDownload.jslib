mergeInto(LibraryManager.library, {
    // Requests a JSON download and reports whether the browser click was dispatched.
    DownloadPlaytestReport: function (jsonPointer, filenamePointer) {
        var link;
        var url;
        try {
            var json = UTF8ToString(jsonPointer);
            var filename = UTF8ToString(filenamePointer);
            url = URL.createObjectURL(new Blob([json], { type: "application/json" }));
            link = document.createElement("a");
            link.href = url;
            link.download = filename;
            link.hidden = true;
            document.body.appendChild(link);
            link.click();
            return 1;
        } catch (error) {
            console.error("Could not request the playtest report download.", error);
            return 0;
        } finally {
            if (link && link.parentNode) link.parentNode.removeChild(link);
            if (url) setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
        }
    }
});
