# PR2_Level_Generator
Procedural level generators for Platform Racing 2.

The hash salts for level uploading and level passwords have been removed, as I am not allowed to share them. If you have access to these, simply edit MapLE.cs and replace "[salt]" with the appropriate values.
As a consequence of this, the tool cannot be used as-is to upload levels. You can, however, save levels locally and load them with PR2 Speedrun Tools (https://www.dropbox.com/s/chgq5fa36hl6th4/PR2%20Speedrun%20Tools.zip?dl=0).

# LevelGenBot
A Discord bot which interfaces with PR2_Level_Generator.

To set up this bot, you must first create a Discord bot via Discord's website. (The Discord .NET library includes a easy tutorial on this: https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/intro.md)
Then, create two files in the folder "LevelGenBot": secrets.txt and special users.txt.
secrets.txt should consist of JSON, with the properties 'bot_token', 'pr2_username', and 'pr2_token', set to your bot's token, the username of the PR2 account to upload levels to, and a login token for said account.
special users.txt should consist of JSON, with the property 'owner', set to your Discord user ID.