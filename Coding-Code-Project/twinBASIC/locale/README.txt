To create a new language pack, simply create a new text file named appropriately according to the IETF language tag in the filename followed by .lang (e.g. 'en-us.lang', 'fr-ca.lang', etc).

Inside that text file, you first need a statement to give the language pack a name that will be visible inside the IDE, e.g.:

FriendlyName:										"English (US)";

You then add properties as seen in the file "en-gb.lang", such as:

MenuCaptionFile:									"My File";

The "en-gb.lang" file is the BASE language pack and contains ALL available localizable strings.  If you don't override a string in your language pack, then it will be populated automatically from the "en-gb" BASE pack.

Note that you should use UTF-8 encoding for the .lang text files.