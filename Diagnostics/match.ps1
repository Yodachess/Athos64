param([string]$Nouveau, [string]$Reference, [int]$Parties = 200, [string]$Cadence = '10+0.1', [int]$Concurrence = 3, [string]$Sortie)
# Match cutechess-cli entre deux dossiers contenant chacun Athos64.exe et le réseau .nnue (version à tester, version de référence)
# Exemple : .\Diagnostics\match.ps1 -Nouveau <dossier> -Reference <dossier> -Sortie <chemin sans extension> (résultat dans .txt, parties dans .pgn)
# Match d'Athos64 (1 thread, profondeur max 20), ouvertures de Diagnostics\ouvertures.pgn
$ouv = Join-Path $PSScriptRoot 'ouvertures.pgn'
$cc = 'C:\Program Files (x86)\Cute Chess\cutechess-cli.exe'
$arguments = @(
  '-engine', 'name=nouveau', "cmd=$Nouveau\Athos64.exe", "dir=$Nouveau",
  '-engine', 'name=reference', "cmd=$Reference\Athos64.exe", "dir=$Reference",
  '-each', 'proto=uci', "tc=$Cadence", 'option.Threads=1', 'option.Depth=20',
  '-openings', "file=$ouv", 'format=pgn', 'order=random', '-repeat',
  '-games', '2', '-rounds', [string]($Parties / 2), '-concurrency', [string]$Concurrence,
  '-draw', 'movenumber=40', 'movecount=8', 'score=10',
  '-resign', 'movecount=4', 'score=800',
  '-recover', '-ratinginterval', '20',
  '-pgnout', "$Sortie.pgn")
& $cc @arguments *> "$Sortie.txt"
