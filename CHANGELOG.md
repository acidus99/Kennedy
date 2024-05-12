# Kennedy Development Changelog 

Keeping track of the work I do on Kennedy.

## 2024-05-12
* Added exponential backoff support to the crawler for "44 SLOW DOWN"

## 2024-01-03
* Fixed bug: Lowercase domain name before searching for it in Domain Backlinks report.

## 2024-01-02
* Added Diff View to Delorean archive. Users can see line-by-line diffs of how text documents changed between unique snapshots. By default, only added/deleted/modified lines are shown. User can toggle seeing all lines in the document.
* Added Diff history view to Delorean archive. Users can see a list of all possible differences between snapshots.
* Added "Previous Snapshot"/"Next Snapshot" links to cached view in Delorean archive. Users can easily page through different archived snapshots for a URL.
* Added "View Differences" link to cached view. Users can easily see te differences between the current and previous unique snapshots.
* Added "View All Differeces" link to URL history view.
* By default, URL history view now only shows unique snapshots (snapshots where the response had not been previously seen), since often URL contents are changing. Users can toggle seeing the entire snapshot history.
* Minor tweaks to URL History view, and Page Info view

## 2023-12-28
* Added Certificate and Key Validator tool. Now users can check if a certificate/key change is innocuous or malicious.

## 2023-11-24
* Added secret inurl: modifier. This is super inefficient so not talking about it too publicly. If you find this, use it!

## 2023-07-12
* Adding Site Search

## 2023-07-08
* Added Title scope. you can use 'intitle:word' or 'intitle:"many words"' on any query to just search a title.
* Added Search Stas view, showing active capsules, urls, and when the search index was last updated.

## 2023-06-28
* Feature: Show simple date, mimetype, redirects, and status code in URL history view

## 2023-06-16
* Re-enabled Plain Text indexing! Search results now return plain text files, though gemtext is favored to rank higher.
* Add "text file sent with wrong mime type" logic, so text files will weird extensions (and thus served with weird/wrong MIME types) are still recognized as text and are indexed by Kennedy (e.g. the CRD and TAB files used in music archive, etc).
* Added file indexing. File's that aren't gemtext, plain text, or images, now get indexed just like images are based on their link text and path. This enables a lot of PDFs, ZIPs, and other files to be discovered via search.
* Added Site Scope! You can use "site:[domain name]" on any query to scope your search to a specific domain.
* Added Filetype Scope. You can "filetype:[extension]" on any query to scope your search. (e.g. "filetype:pdf" to find PDFs)
* Added scope-only queries. You don't have to specify search terms if you are using a scope. (e.g "filetype:pdf amiga" will find PDFs about Amiga, whereas "filetype:pdf" will return all known PDFs. "site:gemi.dev" will return all URLs for gemi.dev)
* Improved Capsule Health report to shows positive messages, separate out 52 GONE responses, and always link to Url Info for problem URLs.
* Improved Url Info view to show connection info, status code, etc.
* (Once again) Improved search result page to show better formatted URL (e.g. "gemi.dev › cgi-bin › wp.cgi").

## 2023-06-06
* Added Capsule Health report, which shows connection errors and status code errors.
* Replaced XXHash with SHA-256 for response and body hashing.

## 2023-05-26
* Improved crawler/indexer so partially downloaded content (like large images) can still be parsed, indexed, and searched.
* Added Capsule Backlinks view to see all external backlinks to a capsule.

## 2023-05-23
* Improved URL history view by organizing captures with year headings.
* Improved search results page and image search results page with a less-cluttered view, based on feedback (Thanks Buckeye Lady!).
* Improved and better organized "Page Info" view.
* Removed Hashtag and @mentions indexes.
* Fixed showing results even if Wikipedia/Gemipedia unavailable. 

## 2023-05-01
* Rebuild entire system to work off Web Archive (WARC) files. Kennedy crawler nows produces WARC files. Search indexer and Archiver ingest WARC files. Additional information like IP address of remote capsules stored in WARC files.
* Converted previous crawl databases to WARC files, allowing easier ingest into Delorean.
* Imported @mozz's late 2021 Gemini archives, which were in WARC format, into Delorean.
* Delorean now stores metaline and response body, allowing storage of 1x, 3x, and 6x response codes.
* Changed archive database so allow for easier calculation of content sizes and savings due to content deduplication.
* Added a /stats/ endpoint, showing stats on URLs, snapshots, and sizes of the archive.

## 2023-03-19
* Massive improvement to Delorean, making it store a history of cached versions of content, and not just the copy found in the most recent crawl.

## 2023-01-27
* Redesign of crawler code which improved speed of the crawler. Robots.txt files are downloaded ondemand instead of requiring a pre-flight step, ensuring that all capsules with Robots.txt are respected

## 2022-08-06
* Updated "Page Info" view to support image meta data (dimensions, format, text used in index) 
* Updated Delorean to work show cached images and other cached, non-text content

## 2022-07-26
* Added image search! Images are indexed based on the text in their file path, as well as the text in all their inbound links.

## 2022-06-04
* Updated searched Also include snippet for Gemipedia about the search query and link to Gemipedia entry.

## 2022-03-01
* Added a "Page Info" view that shows title, language, # lines, size of response, and incoming/outbound links to a page.
* Improved Delorean by adding a "View Cached" link for each page in the "Page Info" view.
* Streamlined the meta data shown on the search results page into a single line and made it a link to "Page Info" view.
* Improved "title" extraction code to use the first header encountered, regardless of level, or alt text from the first pre-formatted section.

## 2022-02-21
* Added Delorean which lets you view cached content from most recent scan by providing a URL.

## 2022-02-14
* Added route/view for showing capsules with valid security.txt files.