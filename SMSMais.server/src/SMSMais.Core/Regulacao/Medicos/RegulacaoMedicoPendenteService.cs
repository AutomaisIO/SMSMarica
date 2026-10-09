using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Medicos;

/// <param name="Origem">"Sistema" = já está na lista do sistema; "Pendente" = alguém já pediu.</param>
/// <param name="Motivo">Por que pareceu o mesmo — o que a tela mostra ao lado ("CRM igual").</param>
public sealed record MedicoParecidoDto(
    string Nome, string Origem, string Valor, double Pontuacao, string Motivo, Guid? PendenteId);

public sealed record CriarMedicoPendenteRequest(
    SistemaRegulacao Sistema, string Nome, string? TipoDocumento, string? NumeroDocumento, string? Especialidade);

public sealed record MedicoPendenteDto(
    Guid Id,
    SistemaRegulacao Sistema,
    string Nome,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Especialidade,
    SituacaoMedicoPendente Situacao,
    string? NomeNoSistema,
    string? Motivo,
    DateTime CriadoEm,
    DateTime? ResolvidoEm,
    /// <summary>O valor que vai no campo de médico da solicitação: <c>pendente:{id}</c>.</summary>
    string Valor);

/// <param name="Acao">"Cadastrado", "JaExistia" ou "Recusado" — e "Pendente" só a partir de
/// "CadastroIncerto" (o regulador conferiu no sistema e o médico NÃO entrou).</param>
/// <param name="NomeNoSistema">Em "Já existia", o nome do cadastro que já estava lá (obrigatório).</param>
public sealed record ResolverMedicoPendenteRequest(SituacaoMedicoPendente Acao, string? NomeNoSistema, string? Motivo);

public interface IRegulacaoMedicoPendenteService
{
    /// <summary>"Já existe?" — o que parece o mesmo médico, na lista do sistema e entre os pendentes.</summary>
    /// <param name="nomesAoVivo">A lista do combo como o sistema a mostra AGORA (no envio). Sem ela,
    /// vale a cópia do catálogo; com ela, os pendentes ficam de fora — a pergunta é "está lá?".</param>
    Task<IReadOnlyList<MedicoParecidoDto>> ParecidosAsync(
        SistemaRegulacao sistema, string nome, string? numeroDocumento, CancellationToken ct,
        IReadOnlyList<string>? nomesAoVivo = null);

    /// <summary>
    /// Cria o pendente — ou devolve o que já existe com o mesmo nome no mesmo sistema, para duas
    /// unidades pedindo o mesmo médico não gerarem dois pedidos de cadastro.
    /// </summary>
    Task<MedicoPendenteDto> CriarAsync(CriarMedicoPendenteRequest req, CancellationToken ct);

    Task<MedicoPendenteDto> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Os médicos na lista do próprio sistema (a cópia do combo "Médico responsável").</summary>
    Task<IReadOnlyList<string>> ListaDoSistemaAsync(SistemaRegulacao sistema, CancellationToken ct);

    Task<IReadOnlyList<MedicoPendenteDto>> ListarAsync(
        SistemaRegulacao? sistema, SituacaoMedicoPendente? situacao, CancellationToken ct);

    /// <summary>O técnico da regulação resolve: cadastrou, já existia ou recusou.</summary>
    Task<MedicoPendenteDto> ResolverAsync(Guid id, ResolverMedicoPendenteRequest req, CancellationToken ct);

    /// <summary>
    /// Antes do Gravar do modal "Adicionar médico": passa o pendente para
    /// <see cref="SituacaoMedicoPendente.CadastroIncerto"/> numa atualização condicional (só se ainda
    /// estiver pendente). É a trava contra duplo clique e contra dois reguladores cadastrando o mesmo
    /// médico — e, se o servidor cair no meio, o estado já é o conservador ("confira no sistema").
    /// </summary>
    Task<MedicoPendenteDto> ReservarCadastroAsync(Guid id, CancellationToken ct);

    /// <summary>O médico apareceu na lista do sistema depois do Gravar: vira "Cadastrado" com o nome de lá.</summary>
    Task<MedicoPendenteDto> ConfirmarCadastroAsync(Guid id, string nomeNoSistema, CancellationToken ct);

    /// <summary>O Gravar não chegou a sair (ou o sistema recusou com mensagem): volta a pendente.</summary>
    Task<MedicoPendenteDto> LiberarCadastroAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// O médico pedido na abertura da solicitação que ainda não está na lista do sistema de destino
/// (ver <see cref="RegulacaoMedicoPendente"/>). Ninguém escreve no SER por aqui: quem cadastra lá é
/// o técnico, pela tela do próprio SER, e aqui só confirma.
/// </summary>
public sealed class RegulacaoMedicoPendenteService(
    SmsMaisDbContext db, IRegulacaoEscopo escopo, IUsuarioAtualAccessor usuarioAtual) : IRegulacaoMedicoPendenteService
{
    public const string PrefixoValor = "pendente:";

    /// <summary>Os tipos do modal "Adicionar médico" do SER.</summary>
    public static readonly string[] TiposDocumento = ["CRM", "CNS", "RG", "CPF", "PMM", "RMS"];

    /// <summary>A partir daqui o nome aparece como "pode ser o mesmo".</summary>
    private const double CorteNome = 0.75;

    public static string Valor(Guid id) => $"{PrefixoValor}{id}";

    /// <summary>O id do pendente, se o valor do campo de médico for um.</summary>
    public static Guid? IdDoValor(string? valor) =>
        valor is not null && valor.StartsWith(PrefixoValor, StringComparison.Ordinal)
        && Guid.TryParse(valor[PrefixoValor.Length..], out var id)
            ? id
            : null;

    public async Task<IReadOnlyList<MedicoParecidoDto>> ParecidosAsync(
        SistemaRegulacao sistema, string nome, string? numeroDocumento, CancellationToken ct,
        IReadOnlyList<string>? nomesAoVivo = null)
    {
        if (string.IsNullOrWhiteSpace(nome)) return [];
        var doc = SemelhancaNome.Digitos(numeroDocumento);
        var achados = new List<MedicoParecidoDto>();
        var aoVivo = nomesAoVivo?.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().ToList();

        foreach (var n in aoVivo ?? await NomesDoSistemaAsync(sistema, ct))
        {
            var p = SemelhancaNome.Pontuacao(nome, n);
            if (p >= CorteNome) achados.Add(new(n, "Sistema", n, p, MotivoNome(nome, n), null));
        }

        // O documento é a pista mais forte, mas só o espelho do SER o tem (metade dos cadastros).
        // CRM do Rio às vezes vem com o "52" na frente e às vezes sem: compara pelo fim.
        if (sistema == SistemaRegulacao.Ser && doc.Length >= 4)
        {
            var comDoc = await db.SerProfissionais.AsNoTracking()
                .Where(p => p.PresenteNoSer && p.Documento != null)
                .Select(p => new { p.Nome, p.Documento })
                .ToListAsync(ct);
            foreach (var p in comDoc)
            {
                var d = SemelhancaNome.Digitos(p.Documento);
                if (d.Length < 4 || !(d == doc || d.EndsWith(doc, StringComparison.Ordinal) || doc.EndsWith(d, StringComparison.Ordinal))) continue;
                // Ao vivo, só vale quem está no combo de hoje: profissional do espelho sem lotação no
                // município não pode ser escolhido como solicitante.
                if (aoVivo is not null && !aoVivo.Any(n => SemelhancaNome.Pontuacao(p.Nome, n) >= 0.99)) continue;
                achados.RemoveAll(a => a.Origem == "Sistema" && a.Nome == p.Nome);
                achados.Add(new(p.Nome, "Sistema", p.Nome, 1.0, $"Documento igual ({p.Documento})", null));
            }
        }

        var pendentes = aoVivo is not null
            ? []
            : await db.RegulacaoMedicosPendentes.AsNoTracking()
                .Where(m => m.Sistema == sistema && m.Situacao == SituacaoMedicoPendente.Pendente)
                .ToListAsync(ct);
        foreach (var m in pendentes)
        {
            var mesmoDoc = doc.Length >= 4 && SemelhancaNome.Digitos(m.NumeroDocumento) == doc;
            var p = mesmoDoc ? 1.0 : SemelhancaNome.Pontuacao(nome, m.Nome);
            if (p < CorteNome) continue;
            achados.Add(new(m.Nome, "Pendente", Valor(m.Id), p,
                mesmoDoc ? "Documento igual — já pedido por outra solicitação" : "Já pedido por outra solicitação",
                m.Id));
        }

        return [.. achados.OrderByDescending(a => a.Pontuacao).ThenBy(a => a.Nome).Take(15)];
    }

    public async Task<MedicoPendenteDto> CriarAsync(CriarMedicoPendenteRequest req, CancellationToken ct)
    {
        if (req.Sistema is not (SistemaRegulacao.Ser or SistemaRegulacao.Sernit))
        {
            // SISREG: o profissional é digitado, não escolhido de lista — não há o que cadastrar.
            throw new ValidacaoException("sistema", "Este sistema não tem lista de médicos: digite o profissional no formulário.");
        }

        var nome = string.Join(' ', (req.Nome ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
        if (nome.Length < 5 || !nome.Contains(' '))
        {
            throw new ValidacaoException("nome", "Digite o nome completo do médico (nome e sobrenome).");
        }
        if (nome.Length > 300) throw new ValidacaoException("nome", "O nome cabe em até 300 caracteres.");

        var tipo = string.IsNullOrWhiteSpace(req.TipoDocumento) ? null : req.TipoDocumento.Trim().ToUpperInvariant();
        var numero = string.IsNullOrWhiteSpace(req.NumeroDocumento) ? null : req.NumeroDocumento.Trim();
        if (tipo is not null && !TiposDocumento.Contains(tipo))
        {
            throw new ValidacaoException("tipoDocumento", $"Tipo de documento deve ser um destes: {string.Join(", ", TiposDocumento)}.");
        }
        if (numero is not null && tipo is null)
        {
            throw new ValidacaoException("tipoDocumento", "Diga de que é o número (CRM, CNS, RG, CPF…).");
        }
        if (numero is { Length: > 40 }) throw new ValidacaoException("numeroDocumento", "O número cabe em até 40 caracteres.");

        // Mesmo médico pedido de novo (por outra unidade, ou num segundo clique): reaproveita.
        var chave = string.Join(' ', SemelhancaNome.Palavras(nome));
        var jaPedido = (await db.RegulacaoMedicosPendentes.AsNoTracking()
                .Where(m => m.Sistema == req.Sistema && m.Situacao == SituacaoMedicoPendente.Pendente)
                .ToListAsync(ct))
            .FirstOrDefault(m => string.Join(' ', SemelhancaNome.Palavras(m.Nome)) == chave);
        if (jaPedido is not null) return Mapear(jaPedido);

        var novo = new RegulacaoMedicoPendente
        {
            Id = Guid.CreateVersion7(),
            Sistema = req.Sistema,
            Nome = nome,
            TipoDocumento = tipo,
            NumeroDocumento = numero,
            Especialidade = string.IsNullOrWhiteSpace(req.Especialidade) ? null : req.Especialidade.Trim().ToUpperInvariant(),
            Situacao = SituacaoMedicoPendente.Pendente,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoMedicosPendentes.Add(novo);
        await db.SaveChangesAsync(ct);
        return Mapear(novo);
    }

    public async Task<IReadOnlyList<string>> ListaDoSistemaAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        [.. (await NomesDoSistemaAsync(sistema, ct)).Where(n => n.Length > 0).OrderBy(n => n, StringComparer.Ordinal)];

    public async Task<MedicoPendenteDto> ObterAsync(Guid id, CancellationToken ct) =>
        Mapear(await db.RegulacaoMedicosPendentes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
               ?? throw new NaoEncontradoException("Médico pendente", id));

    public async Task<IReadOnlyList<MedicoPendenteDto>> ListarAsync(
        SistemaRegulacao? sistema, SituacaoMedicoPendente? situacao, CancellationToken ct)
    {
        var q = db.RegulacaoMedicosPendentes.AsNoTracking();
        if (sistema is { } s) q = q.Where(m => m.Sistema == s);
        if (situacao is { } st) q = q.Where(m => m.Situacao == st);
        return [.. (await q.OrderByDescending(m => m.CriadoEm).Take(500).ToListAsync(ct)).Select(Mapear)];
    }

    public async Task<MedicoPendenteDto> ResolverAsync(
        Guid id, ResolverMedicoPendenteRequest req, CancellationToken ct)
    {
        // Quem confirma o que está no sistema de destino é o técnico da regulação — o mesmo papel
        // que registra o envio.
        await escopo.ExigirAgenteAsync(ct);

        var m = await db.RegulacaoMedicosPendentes.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NaoEncontradoException("Médico pendente", id);
        if (m.Situacao is not (SituacaoMedicoPendente.Pendente or SituacaoMedicoPendente.CadastroIncerto))
        {
            throw new ConflitoException("regulacao.medico_ja_resolvido", "Este médico já foi resolvido por outra pessoa.");
        }

        // "Cadastrei" depois de um Gravar incerto seria afirmar sem conferir: depois de conferir no
        // sistema, o caminho é "Já existia" escolhendo o cadastro (ele entrou) ou "Não entrou".
        if (m.Situacao == SituacaoMedicoPendente.CadastroIncerto && req.Acao == SituacaoMedicoPendente.Cadastrado)
        {
            throw new ValidacaoException(
                "acao", "A plataforma tentou cadastrar e não conseguiu confirmar. Confira no sistema: se o médico "
                        + "está lá, use \"Já existia\" e escolha o cadastro; se não está, \"Não entrou\".");
        }
        if (req.Acao == SituacaoMedicoPendente.Pendente)
        {
            if (m.Situacao != SituacaoMedicoPendente.CadastroIncerto)
            {
                throw new ValidacaoException("acao", "Ação inválida.");
            }
            await LiberarAsync(m, ct);
            return Mapear(m);
        }

        string? nomeFinal;
        switch (req.Acao)
        {
            case SituacaoMedicoPendente.Cadastrado:
                nomeFinal = string.IsNullOrWhiteSpace(req.NomeNoSistema) ? m.Nome : req.NomeNoSistema.Trim().ToUpperInvariant();
                break;
            case SituacaoMedicoPendente.JaExistia:
                if (string.IsNullOrWhiteSpace(req.NomeNoSistema))
                {
                    throw new ValidacaoException("nomeNoSistema", "Escolha o cadastro que já existia no sistema.");
                }
                nomeFinal = req.NomeNoSistema.Trim();
                break;
            case SituacaoMedicoPendente.Recusado:
                if (string.IsNullOrWhiteSpace(req.Motivo))
                {
                    throw new ValidacaoException("motivo", "Diga por que não cadastrou — é o que a unidade vai ler.");
                }
                nomeFinal = null;
                break;
            default:
                throw new ValidacaoException("acao", "Ação inválida.");
        }

        var agora = DateTime.UtcNow;
        m.Situacao = req.Acao;
        m.NomeNoSistema = nomeFinal;
        m.Motivo = string.IsNullOrWhiteSpace(req.Motivo) ? null : req.Motivo.Trim();
        m.ResolvidoEm = agora;
        m.ResolvidoPor = usuarioAtual.UsuarioId;
        m.AtualizadoEm = agora;
        m.AtualizadoPor = usuarioAtual.UsuarioId;

        // Cadastrado ou já existente: toda solicitação que usava o pendente passa a ter o nome que
        // está no sistema — é por ele que o envio acha o médico no combo. Recusado: o campo fica
        // como está, e o "Registrar envio" segue barrado até a unidade escolher outro médico.
        if (nomeFinal is not null) await TrocarNasSolicitacoesAsync(Valor(m.Id), nomeFinal, ct);

        await db.SaveChangesAsync(ct);
        return Mapear(m);
    }

    public async Task<MedicoPendenteDto> ReservarCadastroAsync(Guid id, CancellationToken ct)
    {
        await escopo.ExigirAgenteAsync(ct);
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;
        var linhas = await db.RegulacaoMedicosPendentes
            .Where(x => x.Id == id && x.Situacao == SituacaoMedicoPendente.Pendente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Situacao, SituacaoMedicoPendente.CadastroIncerto)
                .SetProperty(x => x.AtualizadoEm, agora)
                .SetProperty(x => x.AtualizadoPor, quem), ct);
        var m = await db.RegulacaoMedicosPendentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NaoEncontradoException("Médico pendente", id);
        if (linhas == 0)
        {
            throw new ConflitoException(
                "regulacao.medico_ja_resolvido",
                m.Situacao == SituacaoMedicoPendente.CadastroIncerto
                    ? "Já houve uma tentativa de cadastrar este médico no sistema. Confira lá antes de qualquer coisa."
                    : "Este médico já foi resolvido por outra pessoa.");
        }
        return Mapear(m);
    }

    public async Task<MedicoPendenteDto> ConfirmarCadastroAsync(Guid id, string nomeNoSistema, CancellationToken ct)
    {
        var m = await db.RegulacaoMedicosPendentes.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NaoEncontradoException("Médico pendente", id);
        var agora = DateTime.UtcNow;
        m.Situacao = SituacaoMedicoPendente.Cadastrado;
        m.NomeNoSistema = nomeNoSistema.Trim();
        m.Motivo = null;
        m.ResolvidoEm = agora;
        m.ResolvidoPor = usuarioAtual.UsuarioId;
        m.AtualizadoEm = agora;
        m.AtualizadoPor = usuarioAtual.UsuarioId;
        await TrocarNasSolicitacoesAsync(Valor(m.Id), m.NomeNoSistema, ct);
        await db.SaveChangesAsync(ct);
        return Mapear(m);
    }

    public async Task<MedicoPendenteDto> LiberarCadastroAsync(Guid id, CancellationToken ct)
    {
        var m = await db.RegulacaoMedicosPendentes.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NaoEncontradoException("Médico pendente", id);
        if (m.Situacao == SituacaoMedicoPendente.CadastroIncerto) await LiberarAsync(m, ct);
        return Mapear(m);
    }

    // ---------------------------------------------------------------- apoio

    private async Task LiberarAsync(RegulacaoMedicoPendente m, CancellationToken ct)
    {
        m.Situacao = SituacaoMedicoPendente.Pendente;
        m.AtualizadoEm = DateTime.UtcNow;
        m.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    private async Task TrocarNasSolicitacoesAsync(string valorPendente, string nomeFinal, CancellationToken ct)
    {
        var ids = await db.Database
            .SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM smsmarica.regulacao_solicitacao
                WHERE excluido_em IS NULL
                  AND formulario_json -> 'canonico' ->> 'medico_solicitante' = {valorPendente}
                """)
            .ToListAsync(ct);
        if (ids.Count == 0) return;

        var solicitacoes = await db.RegulacaoSolicitacoes.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        foreach (var s in solicitacoes)
        {
            var raiz = JsonNode.Parse(s.FormularioJson) as JsonObject ?? [];
            if (raiz["canonico"] is JsonObject canonico) canonico["medico_solicitante"] = nomeFinal;
            s.FormularioJson = raiz.ToJsonString();
        }
    }

    private async Task<IReadOnlyList<string>> NomesDoSistemaAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        sistema switch
        {
            SistemaRegulacao.Ser => await db.SerCatalogoListas.AsNoTracking()
                .Where(l => l.Lista == "medico").Select(l => l.Rotulo.Trim()).Distinct().ToListAsync(ct),
            SistemaRegulacao.Sernit => await db.SernitCatalogoListas.AsNoTracking()
                .Where(l => l.Lista == "medico").Select(l => l.Rotulo.Trim()).Distinct().ToListAsync(ct),
            _ => [],
        };

    private static string MotivoNome(string digitado, string candidato) =>
        string.Join(' ', SemelhancaNome.Palavras(digitado)) == string.Join(' ', SemelhancaNome.Palavras(candidato))
            ? "Mesmo nome"
            : "Nome parecido (abreviado ou com sobrenome a mais)";

    private static MedicoPendenteDto Mapear(RegulacaoMedicoPendente m) =>
        new(m.Id, m.Sistema, m.Nome, m.TipoDocumento, m.NumeroDocumento, m.Especialidade, m.Situacao,
            m.NomeNoSistema, m.Motivo, m.CriadoEm, m.ResolvidoEm, Valor(m.Id));
}
