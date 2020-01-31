Platform Racing 2: pr2hub.com
All of my levels published through this tool are under the username "R Races". Many of the password-protected levels have a blank password; just click "Enter" without typing anything.

# PR2_Level_Generator
Procedural level generators for Platform Racing 2.

The hash salts for level uploading and level passwords have been removed, as I am not allowed to share them. If you have access to these, simply edit MapLE.cs and replace "[salt]" with the appropriate values.
As a consequence of this, the tool cannot be used as-is to upload levels. You can, however, save levels locally and load them with PR2 Speedrun Tools (https://www.dropbox.com/s/chgq5fa36hl6th4/PR2%20Speedrun%20Tools.zip?dl=0).

# LevelGenBot
A Discord bot which interfaces with PR2_Level_Generator.

To set up this bot, you must first create a Discord bot via Discord's website. (The Discord .NET library includes an easy tutorial on this: https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/intro.md)
Then, create two files in the folder "LevelGenBot": secrets.txt and special users.txt.
secrets.txt should consist of JSON, with the properties 'bot_token', 'pr2_username', and 'pr2_token', set to your bot's token, the username of the PR2 account to upload levels to, and a login token for said account. If you do not create this file, the bot cannot run.
special users.txt should consist of JSON, with the property 'owner', set to your Discord user ID. If you do not create this file, you will not have access to admin commands for the bot.

When running the application, you can provide an argument 'logX' where X is a priority level indicating what should be logged. Possible values are 1 for logging everything, or 2 for logging connection-related events. Default is 2. You can also provide the argument 'bg' to disable reading from the console (allowing the app to run without requiring a window to be open).
