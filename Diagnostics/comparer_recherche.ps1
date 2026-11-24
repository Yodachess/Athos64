param([string]$Dossier, [int]$Profondeur = 12)
# Recherche mono-thread à profondeur fixe sur des positions variées : nœuds, coup et score doivent être identiques d'une version à l'autre si le comportement n'a pas changé
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
$psi.FileName = "$Dossier\Athos64.exe"; $psi.WorkingDirectory = $Dossier
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.UseShellExecute = $false
$p = [Diagnostics.Process]::Start($psi); $w = $p.StandardInput
$w.WriteLine('uci'); $w.WriteLine('setoption name Threads value 1'); $w.WriteLine("setoption name Depth value $Profondeur"); $w.WriteLine('isready')
do { $l = $p.StandardOutput.ReadLine() } until ($l -eq 'readyok')
$totalNoeuds = 0; $totalMs = 0
foreach ($pos in $positions) {
  $w.WriteLine('ucinewgame'); $w.WriteLine('isready')
  do { $l = $p.StandardOutput.ReadLine() } until ($l -eq 'readyok')
  $w.WriteLine($pos); $w.WriteLine("go depth $Profondeur"); $w.Flush()
  $dernier = ''
  do { $l = $p.StandardOutput.ReadLine(); if ($l -like "info depth $Profondeur *") { $dernier = $l } } until ($l -like 'bestmove*')
  $n = [int64]([regex]::Match($dernier, 'nodes (\d+)').Groups[1].Value)
  $t = [int64]([regex]::Match($dernier, ' time (\d+)').Groups[1].Value)
  $totalNoeuds += $n; $totalMs += $t
  "{0,10} noeuds | {1} | {2}" -f $n, ([regex]::Match($dernier, 'score \S+ \S+').Value), (($l -split ' ')[0..1] -join ' ')
}
$w.WriteLine('quit'); $p.WaitForExit(3000) | Out-Null; if (-not $p.HasExited) { $p.Kill() }
"TOTAL {0} noeuds en {1} ms = {2} nps" -f $totalNoeuds, $totalMs, [int]($totalNoeuds * 1000 / [Math]::Max(1, $totalMs))
