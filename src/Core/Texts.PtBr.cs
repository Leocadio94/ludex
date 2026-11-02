namespace Ludex.Core;

public static partial class Texts
{
    private static readonly Dictionary<string, string> PtBr = new()
    {
        ["Open Install Folder"] = "Abrir pasta de instalação",
        ["Open Save Folder"] = "Abrir pasta de saves",
        ["Open Config Folder"] = "Abrir pasta de configurações",
        ["Open Save & Config Folder"] = "Abrir pasta de saves e configurações",

        ["Launch through Steam"] = "Iniciar pela Steam",
        ["Launch through the Epic Games Launcher"] = "Iniciar pelo Epic Games Launcher",
        ["Launch through the Xbox app"] = "Iniciar pelo app Xbox",

        ["Starting..."] = "Iniciando...",
        ["Detecting installed games..."] = "Detectando jogos instalados...",
        ["Matching against the database..."] = "Comparando com o banco de dados...",
        ["{0} games"] = "{0} jogos",
        ["Cancelled"] = "Cancelado",

        ["Loading game database..."] = "Carregando o banco de jogos...",
        ["Downloading game database..."] = "Baixando o banco de jogos...",
        ["Indexing game database..."] = "Indexando o banco de jogos...",
        ["Indexing game database... ({0:N0} games)"] = "Indexando o banco de jogos... ({0:N0} jogos)",

        ["Failed to load game data"] = "Falha ao carregar os dados dos jogos",
        ["Retrying..."] = "Tentando novamente...",
        ["no known save paths"] = "nenhum caminho de save conhecido",
        ["{0} save"] = "{0} save",
        ["{0} saves"] = "{0} saves",
        ["{0} config"] = "{0} configuração",
        ["{0} configs"] = "{0} configurações",
        ["installed as \"{0}\""] = "instalado como \"{0}\"",
        ["Game not found"] = "Jogo não encontrado",
        ["It may have been removed since the last refresh."] = "Ele pode ter sido removido desde a última atualização.",
        ["Back to all games"] = "Voltar para todos os jogos",

        ["Refresh Installed Games"] = "Atualizar jogos instalados",
        ["{0} !games  ·  re-scan Steam, GOG, Epic and Xbox"] = "{0} !games  ·  reescanear Steam, GOG, Epic e Xbox",
        ["Re-detecting installed games..."] = "Detectando os jogos instalados novamente...",
        ["Refresh Game Database"] = "Atualizar banco de jogos",
        ["{0} !refresh  ·  re-download the Ludusavi manifest (~16 MB)"] = "{0} !refresh  ·  baixar o manifesto do Ludusavi de novo (~16 MB)",
        ["Downloading the game database..."] = "Baixando o banco de jogos...",
        ["Search All Games"] = "Pesquisar em todos os jogos",
        ["{0} !all <name>  ·  search the whole database, not just installed games"] = "{0} !all <nome>  ·  pesquisar em todo o banco, não só nos jogos instalados",
        ["Ready — {0} games"] = "Pronto — {0} jogos",
        ["Failed: {0}"] = "Falhou: {0}",
        ["Type at least two characters."] = "Digite pelo menos dois caracteres.",
        ["Database entry  ·  {0} save, {1} config paths"] = "Entrada do banco  ·  {0} de save, {1} de configuração",

        ["{0}   —   not created yet, opens {1}"] = "{0}   —   ainda não criada, abre {1}",
        ["{0}\n\nFrom:\n{1}"] = "{0}\n\nOrigem:\n{1}",
        ["Launch Game"] = "Iniciar jogo",
        ["Could not launch the game"] = "Não foi possível iniciar o jogo",
        ["No known save or config paths"] = "Nenhum caminho de save ou configuração conhecido",
        ["{0} has no usable entry in the Ludusavi manifest."] = "{0} não tem entrada utilizável no manifesto do Ludusavi.",
        ["Folder does not exist"] = "A pasta não existe",
        ["Folder not created yet"] = "Pasta ainda não criada",
        ["Opened {0} instead."] = "{0} foi aberta no lugar.",
        ["Copy Path"] = "Copiar caminho",
        ["Open Folder"] = "Abrir pasta",

        ["Browse installed games and open their save, config or install folders"] = "Veja os jogos instalados e abra as pastas de save, configuração ou instalação",

        ["Stores to scan"] = "Lojas a verificar",
        ["Display"] = "Exibição",
        ["Default order for \"lx\""] = "Ordem padrão para \"lx\"",
        ["Most used first"] = "Mais usados primeiro",
        ["Alphabetical (A-Z)"] = "Alfabética (A-Z)",
        ["Show games with no manifest entry (install folder only)"] = "Mostrar jogos sem entrada no manifesto (só a pasta de instalação)",
        ["Maximum results"] = "Máximo de resultados",
        ["Caching"] = "Cache",
        ["Game database refresh interval (days)"] = "Intervalo de atualização do banco de jogos (dias)",
        ["Installed games cache lifetime (minutes)"] = "Validade do cache de jogos instalados (minutos)",
        ["Use \"lx !refresh\" and \"lx !games\" to refresh immediately."] = "Use \"lx !refresh\" e \"lx !games\" para atualizar na hora.",
    };
}
