@{
    # Local account that runs the tests: a standard user, never an administrator.
    UserName    = 'rimtest'
    FullName    = 'RimWorld test runner'
    Description = 'Limited account for Claude test runs'

    # Folder only this account (plus SYSTEM and Administrators) can use.
    # Connect this folder to Claude sessions.
    # - game: RimWorld with every packed mod in its Mods folder.
    # - profile: the save-data folder RimWorld is launched with.
    # - repo: the Deterrence checkout, linked into game\Mods\Deterrence.
    # - runs: disposable game copies and test profiles.
    # - installers: the Claude Desktop package.
    TestRoot    = 'C:\RimTest'
    Subfolders  = @('game', 'profile', 'repo', 'runs', 'installers')

    # Provision Claude Desktop for every account, so the standard test account gets Cowork.
    InstallClaudeDesktop = $true

    # Let the test account run local scripts such as Build.ps1 without -ExecutionPolicy Bypass.
    SetRemoteSignedPolicy = $true

    # Keep the laptop awake while plugged in, including with the lid closed.
    NeverSleepOnAC = $true
    IgnoreLidOnAC  = $true
}
