# Watched Folders & Dropbox / OneDrive Cloud Safety

## Watched Pattern Folders
- **+ Add Folder** adds a folder to watch. Stitchidex only reads files; it never edits, moves or deletes them.
- **- Remove Folder** removes the selected folder from the catalog (your files stay where they are).
- **Scan / Refresh All** re-scans every watched folder to pick up new or changed designs.
- **Show all folders** clears the folder filter so designs from every folder are shown.
- Hover over any watched folder to see its full path, for example `C:\Users\You\Dropbox\Embroidery\Christmas`. The list scrolls when you have many folders.

## Dropbox and OneDrive "online-only" files
Dropbox and OneDrive can keep files "online-only": the file appears in Explorer but its contents are still in the cloud. Opening such a file makes Dropbox download it. Scanning thousands of online-only files at once could start a huge download and slow your PC down.

Stitchidex uses **Cloud-Safe Mode** by default:
- Before opening any file it checks the Windows cloud attributes (Offline, Recall on open, Recall on data access, Unpinned).
- Online-only files are cataloged from their file name only, without reading their contents. They show the badge **Cloud Only (Not Downloaded)**.
- Files that are already on your computer (Dropbox "Available offline", OneDrive "Always keep on this device") are scanned normally.

To get previews for online-only files, either mark the folder **Available offline / Always keep on this device** in Dropbox or OneDrive, or tick **Allow downloading cloud-only files (Dropbox/OneDrive)** in the sidebar and click **Scan / Refresh All**.

## Cancel Scan
A scan can be stopped at any time with **Cancel Scan** in the status row, or by pressing `Esc`. Designs scanned so far stay in the catalog.
