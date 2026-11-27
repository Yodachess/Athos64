param([string]$Moteur, [string[]]$Options = @(), [int]$Temps = 5000, [int]$DepuisProfondeur = 6)
# Facteur de branchement effectif d'un moteur UCI (2026-11-27) : sur les 10 positions de comparer_recherche.ps1,
# "go movetime" puis relevé des nœuds (cumulés) à chaque profondeur terminée.
# Facteur d'une position = (nœuds à la dernière profondeur / nœuds à DepuisProfondeur) ^ (1 / écart de profondeur).
# Exemple : .\Diagnostics\facteur_branchement.ps1 -Moteur <exe> -Options 'Threads=1','Hash=128'
$positions = @(
 'position fen r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10',
 'position fen r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1',
 'position fen 8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1',
 'position fen n1n1r1k1/PPP2ppp/8/8/8/8/p1p2PPP/N1N1R1K1 w - - 0 1',
 'position fen rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8',
 'position startpos moves e2e4 e7e5 g1f3 b8c6 f1b5 a7a6',
 'position fen 8/8/4k3/8/2p5/8/B2K4/8 w - - 0 1',
 'position fen 2r3k1/pp3ppp/8/3q4/3P4/2P2Q2/PP3PPP/2R3K1 w - - 0 1',
 'position fen r1bq1rk1/ppp2ppp/2n2n2/3pp3/1bPP4/2N1PN2/PP3PPP/R1BQKB1R w KQ - 0 7',
 'position fen r2q1rk1/pp2bppp/2n1pn2/2pp4/3P1B2/2PBPN2/PP1N1PPP/R2Q1RK1 w - - 0 10')
$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = $Moteur; $psi.WorkingDirectory = Split-Path $Moteur
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.UseShellExecute = $false
$p = [Diagnostics.Process]::Start($psi); $w = $p.StandardInput
function Lire-Ligne {   # ReadLine rend null quand le moteur ferme sa sortie : on s'arrête au lieu de boucler
  $ligne = $p.StandardOutput.ReadLine()
  if ($null -eq $ligne) { throw "Le moteur a fermé sa sortie (code $(if ($p.HasExited) { $p.ExitCode }))." }
  $ligne
}
$w.WriteLine('uci')
do { $l = Lire-Ligne } until ($l -eq 'uciok')
foreach ($o in $Options) { $nom, $val = $o -split '=', 2; $w.WriteLine("setoption name $nom value $val") }
$w.WriteLine('isready')
do { $l = Lire-Ligne } until ($l -eq 'readyok')
$facteurs = @(); $totalNoeuds = 0; $totalMs = 0; $totalProf = 0
foreach ($pos in $positions) {
  $w.WriteLine('ucinewgame'); $w.WriteLine('isready')
  do { $l = Lire-Ligne } until ($l -eq 'readyok')
  $w.WriteLine($pos); $w.WriteLine("go movetime $Temps"); $w.Flush()
  $noeuds = @{}; $dernier = ''
  do {
    $l = Lire-Ligne
    $m = [regex]::Match($l, '^info .*\bdepth (\d+)\b.*\bnodes (\d+)')
    if ($m.Success -and $l -match ' pv ' -and $l -notmatch 'bound') { $noeuds[[int]$m.Groups[1].Value] = [int64]$m.Groups[2].Value; $dernier = $l }
  } until ($l -like 'bestmove*')
  $dmax = [int]($noeuds.Keys | Measure-Object -Maximum).Maximum
  $t = [int64]([regex]::Match($dernier, '\btime (\d+)').Groups[1].Value)
  $totalNoeuds += $noeuds[$dmax]; $totalMs += $t; $totalProf += $dmax
  $f = if ($noeuds.ContainsKey($DepuisProfondeur) -and $dmax -gt $DepuisProfondeur) { [Math]::Pow($noeuds[$dmax] / $noeuds[$DepuisProfondeur], 1.0 / ($dmax - $DepuisProfondeur)) } else { [double]::NaN }
  $facteurs += $f
  "{0,-60} prof {1,2}  noeuds {2,10}  facteur {3:N2}" -f ($pos.Substring(0, [Math]::Min(60, $pos.Length))), $dmax, $noeuds[$dmax], $f
}
$w.WriteLine('quit'); $p.WaitForExit(3000) | Out-Null; if (-not $p.HasExited) { $p.Kill() }
$valides = $facteurs | Where-Object { -not [double]::IsNaN($_) }
"MOYENNE prof {0:N1}  facteur (moyenne géométrique) {1:N2}  vitesse {2} nps" -f ($totalProf / $positions.Count), [Math]::Exp((($valides | ForEach-Object { [Math]::Log($_) }) | Measure-Object -Sum).Sum / $valides.Count), [int]($totalNoeuds * 1000 / [Math]::Max(1, $totalMs))
