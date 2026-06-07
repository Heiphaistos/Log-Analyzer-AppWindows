namespace WinLogAnalyzer.Core.Tasks;

/// <summary>
/// Produit une remediation precise pour n'importe quel code Windows, par regles en cascade :
/// code Win32 exact -> Windows Update -> MSI -> facilite HRESULT -> categorie NTSTATUS -> generique.
/// </summary>
public static class RemediationEngine
{
    /// <summary>Remediation ciblee pour un code (jamais vide).</summary>
    public static string Remediate(uint u)
    {
        uint win32 = (u & 0xFFFF0000) == 0x80070000 ? (u & 0xFFFF) : u;

        if (win32 <= 0xFFFF && Win32Exact.TryGetValue(win32, out var w)) return w;
        if (u is >= 0x80240000 and <= 0x8024FFFF) return WindowsUpdate(u);
        if (IsMsi(u, win32)) return Msi();

        // HRESULT : remediation par facilite.
        if ((u & 0x80000000) != 0 && (u & 0xF0000000) is not (0xC0000000 or 0xD0000000))
        {
            uint fac = (u >> 16) & 0x1FFF;
            var byFac = Facility(fac);
            if (byFac is not null) return byFac;
        }

        if ((u & 0xF0000000) is 0xC0000000 or 0xD0000000) return NtStatus(u);

        if (u == 0 || (u >= 0x41300 && u <= 0x4131F)) return "Aucune action — etat normal.";
        return Generic;
    }

    // ===== Win32 exacts (les plus courants, remediation precise) =====
    private static readonly Dictionary<uint, string> Win32Exact = new()
    {
        [1] = "1. Executer l'action manuellement pour voir l'erreur reelle. 2. Verifier arguments et droits.",
        [2] = "1. Verifier le chemin du fichier/programme. 2. Guillemets si espaces. 3. Confirmer que le fichier existe.",
        [3] = "1. Verifier le dossier (chemin / 'Demarrer dans'). 2. Recreer le dossier manquant.",
        [4] = "1. Trop de fichiers ouverts : fermer des applications. 2. Redemarrer le service concerne.",
        [5] = "1. 'Executer avec les autorisations maximales'. 2. Verifier le compte d'execution. 3. Verifier les ACL (clic droit -> Securite) du fichier/dossier cible.",
        [8] = "1. Memoire insuffisante : fermer des applications. 2. Augmenter le fichier d'echange.",
        [14] = "1. Memoire insuffisante. 2. Augmenter la RAM/fichier d'echange.",
        [19] = "1. Support en lecture seule : retirer la protection en ecriture. 2. Verifier les droits du volume.",
        [20] = "1. Peripherique introuvable : verifier le branchement et le driver.",
        [21] = "1. Peripherique pas pret : verifier l'alimentation/connexion. 2. Mettre a jour le driver.",
        [32] = "1. Fichier verrouille par un autre processus : identifier via Resource Monitor. 2. Planifier quand le fichier est libre.",
        [33] = "1. Verrou de plage de fichier : fermer l'app qui le tient.",
        [53] = "1. Chemin reseau introuvable : verifier la connectivite et le partage. 2. Verifier le nom UNC.",
        [55] = "1. Ressource reseau indisponible : verifier le partage distant et les droits.",
        [64] = "1. Nom reseau indisponible : verifier serveur/partage. 2. Verifier la session SMB.",
        [67] = "1. Nom reseau introuvable : verifier le partage et le DNS.",
        [80] = "1. Le fichier existe deja : changer la cible ou autoriser l'ecrasement.",
        [87] = "1. Parametre invalide : verifier les arguments passes a la commande.",
        [112] = "1. Espace disque insuffisant : liberer de l'espace (Nettoyage de disque).",
        [123] = "1. Nom/chemin contient des caracteres invalides : corriger le chemin.",
        [126] = "1. Module/DLL introuvable : reinstaller le logiciel, installer le runtime requis (VC++/.NET).",
        [127] = "1. Procedure introuvable dans une DLL : versions incompatibles -> reinstaller/mettre a jour.",
        [145] = "1. Repertoire non vide : vider avant suppression.",
        [183] = "1. Le fichier existe deja : gerer le conflit (renommer/ecraser).",
        [193] = "1. Application non valide (32/64 bits ou corrompue) : reinstaller, verifier l'architecture.",
        [225] = "1. Operation bloquee par l'antivirus : ajouter une exclusion si legitime.",
        [267] = "1. Nom de repertoire invalide : verifier que la cible est bien un dossier.",
        [1053] = "1. Le service n'a pas repondu au demarrage a temps : verifier sa charge, augmenter ServicesPipeTimeout si justifie.",
        [1058] = "1. Service desactive : l'activer (services.msc). 2. Verifier ses dependances.",
        [1067] = "1. Le processus du service s'est arrete : consulter ses logs, mettre a jour le logiciel.",
        [1068] = "1. Dependance de service non demarree : demarrer la dependance d'abord.",
        [1069] = "1. Echec d'ouverture de session du service : verifier compte/mot de passe d'execution.",
        [1075] = "1. Dependance inexistante/supprimee : corriger la config du service.",
        [1392] = "1. Fichier/repertoire corrompu : chkdsk /f. 2. Restaurer depuis une sauvegarde.",
        [1450] = "1. Ressources systeme insuffisantes : redemarrer, chercher une fuite de handles.",
        [1453] = "1. Quota de pagination insuffisant : augmenter le fichier d'echange.",
        [1455] = "1. Fichier d'echange trop petit : l'augmenter.",
        [1460] = "1. Operation expiree (timeout) : augmenter le delai, verifier les dependances lentes.",
        [1602] = "1. Installation annulee par l'utilisateur : relancer si involontaire.",
        [1603] = "1. Echec fatal MSI : relancer en admin avec log (msiexec /i pkg /l*v log.txt), liberer de l'espace, fermer les apps en conflit.",
        [1605] = "1. Produit non installe : verifier le code produit, reinstaller.",
        [1618] = "1. Une autre installation est en cours : attendre sa fin puis relancer.",
        [1619] = "1. Package MSI introuvable/inaccessible : verifier le chemin et les droits.",
        [1638] = "1. Une autre version est deja installee : desinstaller l'ancienne d'abord.",
        [1641] = "1. Redemarrage initie pour finaliser l'installation.",
        [3010] = "1. Installation reussie : redemarrer pour finaliser.",
        [0x102] = "1. Action trop longue/bloquee (timeout) : augmenter le delai, optimiser le script.",
    };

    private static bool IsMsi(uint u, uint win32)
        => u == 0x80070641 || u == 0x80070643 || (win32 is >= 1601 and <= 1654);

    private static string Msi() =>
        "1. Relancer en admin avec journal verbeux : msiexec /i package.msi /l*v log.txt. " +
        "2. Lire l'erreur dans log.txt. 3. Verifier le service 'Windows Installer' (net start msiserver). " +
        "4. Liberer de l'espace disque. 5. Outil de depannage Installation/Desinstallation Microsoft.";

    private static string WindowsUpdate(uint u) =>
        "1. Lancer l'outil de depannage Windows Update (Parametres -> Systeme -> Resolution des problemes). " +
        "2. Reinitialiser les composants : net stop wuauserv bits, renommer C:\\Windows\\SoftwareDistribution, redemarrer les services. " +
        "3. DISM /Online /Cleanup-Image /RestoreHealth puis sfc /scannow. " +
        "4. Verifier connexion/proxy (netsh winhttp show proxy). 5. Installer manuellement via le Catalogue Microsoft Update.";

    private static string? Facility(uint fac) => fac switch
    {
        1 => "1. Erreur RPC : verifier que le service 'Appel de procedure distante (RPC)' tourne. 2. Verifier le reseau/pare-feu si distant.",
        3 => "1. Erreur de stockage : chkdsk /f, verifier SMART (CrystalDiskInfo), sauvegarder.",
        4 => "1. Composant COM defaillant : reinstaller/reparer le logiciel. 2. Re-enregistrer la DLL (regsvr32). 3. Verifier l'architecture 32/64 bits.",
        10 => "1. Securite/SSPI : verifier les certificats (certlm.msc), la synchro horaire (w32tm /resync) et les protocoles TLS actives.",
        0x9 => "1. Certificat : verifier validite/chaine/date du certificat. 2. Reinstaller le certificat.",
        0xB => "1. Schannel/TLS : activer TLS 1.2/1.3, mettre a jour Windows, verifier les suites de chiffrement.",
        0x100 => Msi(),
        _ => null
    };

    private static string NtStatus(uint u) => u switch
    {
        0xC0000005 => "1. Plantage (violation d'acces) : mettre a jour l'application. 2. Mettre a jour les drivers. 3. Tester la RAM (mdsched).",
        0xC0000017 or 0xC000009A => "1. Memoire/ressources insuffisantes : fermer des apps, augmenter le fichier d'echange, ajouter de la RAM.",
        0xC0000135 => "1. DLL introuvable au demarrage : reinstaller l'app, installer le runtime (.NET/VC++).",
        0xC0000142 => "1. Echec d'initialisation de DLL : reparer le runtime, sfc /scannow, reinstaller l'app.",
        0xC000007B => "1. Image non valide (melange 32/64 bits ou DLL corrompue) : reinstaller l'app et les runtimes correspondants.",
        0xC0000374 => "1. Corruption du tas (bug/materiel) : mettre a jour l'app, tester la RAM, verifier les drivers.",
        0xC0000409 => "1. Depassement de tampon de pile (securite) : mettre a jour l'app, scanner les malwares.",
        0xC00000FD => "1. Depassement de pile : bug applicatif -> mettre a jour, signaler a l'editeur.",
        0xC000021A => "1. Erreur systeme fatale : demarrer en mode sans echec, sfc /scannow, restauration systeme.",
        0xC0000022 => "1. Acces refuse (noyau) : 'autorisations maximales', verifier compte et ACL.",
        _ => "1. Erreur noyau/driver : mettre a jour l'app et les drivers. 2. Tester RAM (mdsched) et disque (chkdsk). 3. Consulter un minidump si BSOD."
    };

    private const string Generic =
        "1. Executer l'action manuellement pour observer l'erreur. 2. Consulter les logs de l'application. " +
        "3. Verifier chemins, droits et dependances. 4. Mettre a jour/reinstaller le logiciel concerne.";
}
