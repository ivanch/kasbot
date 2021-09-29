# KasBot
## Bot do Kasino para o Discord.

## Executar
1. Tenha `python3` e `pip3` instalado.
2. `git checkout https://github.com/ivanch/kasbot`
3. `pip3 install -r requirements.txt`
3. Exporte o token do seu bot com `export TOKEN=[token]`
4. `python main.py`

#### Keepalive
Para executar com *keepalive* (manter o servidor vivo com um servidor web):
1. Instale o flask utilizando `pip3 install flask`
2. Defina uma variável de ambiente *alive* com `export alive`
3. Execute os [passos acima](#executar).

## Comandos
* `kasino`
    * Toca o famoso SABADAÇO.
* `shakeit`
    * Toca o hit SHAKE IT.
* `jetmusic`
    * NAS PISTAS DO JET MUSIC!
* `pare`
    * Para de tocar e desconecta.
* `companhia`
    * Entra no canal e te faz companhia, sem falar nem ouvir nada, apenas o doce som do silêncio.

## Player
* `-play [url/query]` (`-p`)
    * Toca música do u2b.
* `-stop`
    * Para de tocar.
* `-leave` (`-disconnect`, `-dc`, `-q`)
    * Desconecta do canal.
* `-volume [volume]`
    * Ajuda volume (WIP).
* `-now` (`-current`, `-playing`)
    * Mostra o que tá tocando atualmente.
* `-pause`
    * Pausa.
* `-resume` (`-r`)
    * Dá play de novo (depois de pausar).
* `-skip` (`-next`, `-s`, `-n`)
    * Pula pra próxima música na fila.
* `-queue`
    * Mostra a fila de músicas.
* `-shuffle` (`-sf`)
    * Embaralha as músicas dentro da fila.
* `-remove [indice]`
    * Remove uma música baseada no indice na fila.
* `-loop` (`-l`)
    * Deixa a música atual em loop.
