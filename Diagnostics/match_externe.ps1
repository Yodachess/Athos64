param(
  [string]$Athos,                                   # dossier contenant Athos64.exe, BrunoNNUE.dll et le réseau .nnue
  [string]$Adversaire = 'C:\Users\bruno\OneDrive\Programmation\Bruno_Cplusplus\OutilsEchecsBruno\OutilsEchecs\Moteurs_UCI\dragon\dragon-64bit-avx2_Prefere.exe',
  [string]$NomAdversaire = 'Dragon',
  [string[]]$OptionsAdversaire = @('option.Hash=128', 'option.OwnBook=false'),
  [int]$Parties = 50, [string]$Cadence = '30+0.3', [int]$Concurrence = 3, [string]$Sortie)
# Match cutechess-cli d'Athos64 contre un moteur UCI externe (Dragon par défaut), 2026-11-27.
# Conditions égales : 1 thread chacun, pas de ponder (cutechess ne le demande pas), pas de livre propre
# à l'adversaire (OwnBook=false) : les deux jouent les ouvertures de Diagnostics\ouvertures.pgn, des deux couleurs.
# Table : 128 Mo pour l'adversaire, comme la table fixe d'Athos64.
# Exemple : .\Diagnostics\match_externe.ps1 -Athos <dossier> -Sortie <chemin sans extension> (résultat dans .txt, parties dans .pgn)
$ouv = Join-Path $PSScriptRoot 'ouvertures.pgn'
$cc = 'C:\Program Files (x86)\Cute Chess\cutechess-cli.exe'
$dirAdv = Split-Path $Adversaire
$arguments = @(
  '-engine', 'name=Athos64', "cmd=$Athos\Athos64.exe", "dir=$Athos", 'option.Depth=20',
  '-engine', "name=$NomAdversaire", "cmd=$Adversaire", "dir=$dirAdv") + $OptionsAdversaire + @(
  '-each', 'proto=uci', "tc=$Cadence", 'option.Threads=1',
  '-openings', "file=$ouv", 'format=pgn', 'order=random', '-repeat',
  '-games', '2', '-rounds', [string]($Parties / 2), '-concurrency', [string]$Concurrence,
  '-draw', 'movenumber=40', 'movecount=8', 'score=10',
  '-resign', 'movecount=4', 'score=800',
  '-recover', '-ratinginterval', '10',
  '-pgnout', "$Sortie.pgn")
& $cc @arguments *> "$Sortie.txt"
