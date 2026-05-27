Open English Bible
==================

About
-----

The Open English Bible is the anticipated end product of a project intended to create an English translation of the Bible that is:

* under a licence enabling the maximum reuse, remixing and sharing without requiring the payment of royalties or the obtaining of permission from copyright holders; and
* a translation reflecting modern English usage and Biblical scholarship.

The New Testament of the OEB is being formed by editing the public domain Twentieth Century New Testament, which was a new translation of the New Testament published in the early twentieth century, based on the Greek text of Westcott and Hort.

The Hebrew Bible is being formed by editing a number of public domain translations done by John E McFadyen, Charles F Kent and James Moffatt.

As such, the OEB as a translation does not stand within the Tyndale tradition but has a separate tradition in a similar manner to the NIV and New English Bible.

Our website is at http://openenglishbible.org

The Open English Bible is under the Creative Commons Zero (CCO) license.

This site
---------

This source tree contains:

`artifacts/`
The final generated documents. This is probably what you want if you want to use the OEB. The subdirectories are marked as 'release' which has the books in the OEB release, and 'development' which has all books, no matter how partial or rough.

`source/`
These are the source files we are working from. They are USFM files with a lightweight layer of markup to handle variations.

`build-release.sh`
A bash script to generate a release version from the source.

`update-development-artifacts.py`
A python3 script to generate usfm and rtf files for all the books, whether in development or release.

To make these scripts work, you will need to have the USFM-Tools git repository from https://github.com/openenglishbible/USFM-Tools in this top level directory as the `USFM-Tools` folder.

OEB Desktop Reader
------------------

This fork includes a desktop Bible reader application built with F# and WinForms, featuring text-to-speech capabilities.

`OEB-Desktop/`
A Windows desktop application for reading and listening to the Open English Bible. Features include:

* **Book Navigation**: Browse books organized by biblical sections (Pentateuch, History, Poetry, Prophets, Gospels, Letters, Apocalypse)
* **Chapter Selection**: Click on any chapter in the tree view to display its content
* **Text-to-Speech**: Read Bible chapters aloud using multiple speech engines:
  - **Windows Offline**: Uses built-in Windows speech synthesis (System.Speech)
  - **Offline Neural**: Uses KokoroSharp for high-quality local neural voice synthesis
  - **Azure Neural**: Uses Microsoft Azure Cognitive Services Speech for cloud-based neural voices
* **Voice Selection**: Choose from available voices for your selected speech engine
* **Playback Controls**: Play, Stop, Previous Chapter, Next Chapter navigation
* **Speed Control**: Adjustable speech rate from -10 to +10
* **Search**: Filter books and chapters by name or content

### Building and Running

Prerequisites:
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
* Windows operating system (WinForms required)

Build and run:
```
cd OEB-Desktop
dotnet run
```

### Azure Neural Voices Setup

To use Azure Neural voices, set the following environment variables:
* `OEB_AZURE_SPEECH_KEY` - Your Azure Speech Service subscription key
* `OEB_AZURE_SPEECH_REGION` - Your Azure Speech Service region (e.g., `eastus`)

### Project Structure

`OEB-Desktop/Program.fs` - Main application source code
`OEB-Desktop/OEB-Desktop.fsproj` - Project file with dependencies including KokoroSharp, Microsoft.CognitiveServices.Speech, and System.Speech




