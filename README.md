# Darkbulb
Darkbulb is a Discord bot that scrapes the Riot Career page to see any changes in status (addition or removal of jobs)

There are 3 json files used by the bot: 
- channels_config, which stores the channel ID for bot output for every guild ID (Discord server ID)
- config.json, which stores the token for the bot
- careers.json, whichs stores the job ID as a key and job info as subfields


# Building the Bot
You can now use Docker compose to build/run the bot. You'll need to rename the .envtemplate to .env and fill in the environment variables:
- `BOT_TOKEN` This will be your bot's discord token
- `APP_ID` This is your discord bot's application Id.

# Adding the bot to the server
You can add the discord bot to the server like you normally would for other bots.
However the bot will only send messages to servers & channels in it's channel_config.json file. You'll need to manually add the servers to this json.

# Commands
- /changelog {JOB ID} - Sends the changelog for a specific job id
- /exportg - Exports the job changelog history in a CSV
- /set-channel Set's the channel for changelog message output.
