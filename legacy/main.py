import os
import discord

from discord.ext import commands
from player import Music
from kasino import Kasino

from dotenv import load_dotenv
load_dotenv()

# Setup suffixes, set env suffixes if they're set, otherwise default
MUSIC_SUFFIX = os.getenv("MUSIC_SUFFIX") or "-"
KASINO_SUFFIX = os.getenv('KASINO_SUFFIX') or ""

client = commands.Bot(command_prefix=[MUSIC_SUFFIX, KASINO_SUFFIX], description='Kasino DEEJAY.')
music = Music(client)
kasino = Kasino(client)
client.add_cog(music)
client.add_cog(kasino)

kasino_commands = []
for cmd in kasino.get_commands():
	kasino_commands.append(KASINO_SUFFIX + cmd.name)

@client.event
async def on_ready():
	print('Logged in as {0.user}!'.format(client))

@client.event
async def on_message(message: discord.Message):
	# Verifica se é um comando ou não
	if message.content in kasino_commands or message.content.startswith(MUSIC_SUFFIX):
		await client.process_commands(message)

# #
# Init KASINÂO
# #

alive = os.getenv('alive')
if alive is not None:
	from utils.keepalive import keep_alive
	keep_alive()

client.run(os.getenv("TOKEN"))
