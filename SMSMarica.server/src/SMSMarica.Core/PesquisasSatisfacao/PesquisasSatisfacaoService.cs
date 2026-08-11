using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Atendimentos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.PesquisasSatisfacao.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.PesquisasSatisfacao;

/// <summary>
/// Pesquisa de satisfação por atendimento. Ver <see cref="PesquisaSatisfacao"/> para o desenho
/// do token e <see cref="IPesquisasSatisfacaoService.JanelaDias"/> para o prazo.
/// </summary>
public sealed class PesquisasSatisfacaoService(
    SmsMaricaDbContext db,
    IAtendimentosService atendimentos,
    IUsuarioAtualAccessor usuarioAtual,
    IPacientesService pacientes,
    IWhatsAppCliente whatsApp,
    IConfiguration configuration) : IPesquisasSatisfacaoService
{
    /// <summary>
    /// Perguntas aceitas. Fechar a lista é o que impede uma tela adulterada de gravar campo
    /// inventado no meio da série — e o que denuncia, no deploy, que a tela mudou e o servidor não.
    /// </summary>
    private static readonly HashSet<string> PerguntasValidas =
    [
        "geral", "espera", "equipe", "informacoes", "confianca", "limpeza", "comentario",
    ];

    private const int TamanhoMaximoComentario = 2_000;

    public async Task<PesquisaPublicaDto> ObterPorTokenAsync(Guid token, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.AsNoTracking().FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        return new PesquisaPublicaDto(
            p.UnidadeNome,
            p.AtendimentoEm,
            p.ExpiraEm,
            Expirada: DateTime.UtcNow > p.ExpiraEm,
            JaRespondida: p.RespondidaEm is not null,
            p.InstrumentoVersao);
    }

    public async Task ResponderPorTokenAsync(
        Guid token, IReadOnlyDictionary<string, string> respostas, string? ip, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        Gravar(p, respostas, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResponderPeloAppAsync(
        Guid pacienteId, Guid encounterId, IReadOnlyDictionary<string, string> respostas, string? ip,
        CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao
                    .FirstOrDefaultAsync(x => x.EncounterId == encounterId && x.PatientId == pacienteId, ct)
                ?? await CriarAsync(pacienteId, encounterId, ct);

        Gravar(p, respostas, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.EncounterId == encounterId, ct);
        var jaEnviada = p?.EnviadaEm is not null;

        if (p is null)
        {
            p = await CriarAsync(pacienteId, encounterId, ct);
        }
        else if (p.PatientId != pacienteId)
        {
            // O atendimento é de outra pessoa: enviar aqui mandaria o convite ao paciente errado.
            throw new ConflitoException(
                "pesquisa.paciente_divergente",
                "Este atendimento pertence a outro paciente.");
        }

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para avaliar este atendimento já passou.");

        if (p.RespondidaEm is not null)
            throw new ConflitoException("pesquisa.ja_respondida", "Este atendimento já foi avaliado.");

        p.EnviadaEm = DateTime.UtcNow;
        p.EnviadaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return new EnvioPesquisaDto(p.Id, MontarUrl(p.Id), jaEnviada);
    }

    public async Task<EnvioPesquisaDto> EnviarAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var envio = await PrepararEnvioAsync(pacienteId, encounterId, ct);
        var p = await db.PesquisasSatisfacao.AsNoTracking().FirstAsync(x => x.Id == envio.PesquisaId, ct);
        var paciente = await pacientes.ObterPorIdAsync(pacienteId, ct);

        // Mesma régua do resto das comunicações: contato verificado primeiro, qualquer celular
        // do cadastro depois. Aqui NÃO se exige verificação — a pesquisa não carrega resultado
        // nem laudo, e exigir contato verificado deixaria de fora justamente quem a recepção
        // ainda não alcançou.
        var telefone = paciente.TelefoneVerificado
            ?? new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal, paciente.TelefoneResidencial }
                .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
            throw new ConflitoException(
                "pesquisa.sem_telefone", "Paciente sem número de celular válido para receber a pesquisa.");

        // O corpo do template diz "recebido em nossa unidade {{2}}". Sem o nome real, a frase
        // sairia "em nossa unidade nossa unidade" — e, pior, afirmaria ao paciente algo que não
        // conferimos. Recusa é o comportamento certo: a unidade vem do serviceProvider do
        // Encounter (ADR-0039) e está preenchida em 100% dos atendimentos recentes; se faltar,
        // é sinal de dado incompleto, não de mensagem a improvisar.
        if (string.IsNullOrWhiteSpace(p.UnidadeNome))
            throw new ConflitoException(
                "pesquisa.sem_unidade",
                "O atendimento não tem unidade registrada — não dá para citar o local na mensagem.");

        var template = configuration["Pesquisa:Template"] ?? "pesquisa_de_satisfacao_2";
        var idioma = configuration["Pesquisa:TemplateIdioma"] ?? "pt_BR";

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            TelefoneWhatsApp.NormalizarNonoDigito(telefone!),
            template,
            idioma,
            [
                Tratamento(paciente.NomeCompleto, paciente.Sexo),
                p.UnidadeNome,
                FusoBrasilia.ParaExibicao(p.AtendimentoEm).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")),
            ],
            // Só o botão de URL entra no payload: as duas respostas rápidas do template são
            // estáticas. O índice 0 é o do botão de link, que é o primeiro do template aprovado.
            [new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, envio.PesquisaId.ToString())],
            pacienteId,
            ct);

        if (!resultado.Ok)
            throw new ConflitoException("pesquisa.falha_envio", resultado.Erro ?? "Falha ao enviar a pesquisa.");

        return envio;
    }

    /// <summary>Sr./Sra. + primeiro nome; sem sexo no cadastro, só o primeiro nome.</summary>
    private static string Tratamento(string? nomeCompleto, Sexo? sexo)
    {
        var primeiro = (nomeCompleto ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "paciente";
        return sexo switch
        {
            Sexo.Masculino => $"Sr. {primeiro}",
            Sexo.Feminino => $"Sra. {primeiro}",
            _ => primeiro,
        };
    }

    /// <summary>
    /// Cria a pesquisa a partir do atendimento no hub. Passa pelo serviço de atendimentos do
    /// PACIENTE de propósito: é o que garante que ninguém abra pesquisa de encontro alheio.
    /// </summary>
    private async Task<PesquisaSatisfacao> CriarAsync(Guid pacienteId, Guid encounterId, CancellationToken ct)
    {
        var lista = await atendimentos.ObterPorPacienteAsync(pacienteId, ct);
        var a = lista.FirstOrDefault(x => x.Id == encounterId)
            ?? throw new NaoEncontradoException("Atendimento", encounterId);

        // O fim do atendimento é o marco. Sem ele o atendimento não terminou — e não há o que avaliar.
        var fim = (a.Fim ?? a.Inicio)?.UtcDateTime
            ?? throw new ConflitoException(
                "pesquisa.atendimento_sem_data", "Este atendimento ainda não tem data de saída.");

        var p = new PesquisaSatisfacao
        {
            Id = Guid.CreateVersion7(),
            PatientId = pacienteId,
            EncounterId = encounterId,
            // ADR-0039: a unidade é dimensão própria, não deriva do meta.source. Guardar o nome
            // aqui congela o que valia no atendimento — unidade renomeada depois não reescreve o
            // passado de quem já respondeu, e a mensagem cita o lugar onde a pessoa esteve.
            UnidadeNome = a.UnidadeNome,
            AtendimentoEm = fim,
            ExpiraEm = fim.AddDays(IPesquisasSatisfacaoService.JanelaDias),
            InstrumentoVersao = IPesquisasSatisfacaoService.InstrumentoVersaoAtual,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.PesquisasSatisfacao.Add(p);
        return p;
    }

    /// <summary>Régua única de gravação — as duas portas (link e app) passam por aqui.</summary>
    private static void Gravar(PesquisaSatisfacao p, IReadOnlyDictionary<string, string> respostas, string? ip)
    {
        if (p.RespondidaEm is not null)
            throw new ConflitoException("pesquisa.ja_respondida", "Esta pesquisa já foi respondida.");

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para responder já passou.");

        var desconhecidas = respostas.Keys.Where(k => !PerguntasValidas.Contains(k)).ToList();
        if (desconhecidas.Count > 0)
            throw new ValidacaoException(
                "pesquisa.pergunta_desconhecida",
                $"Pergunta(s) não reconhecida(s): {string.Join(", ", desconhecidas)}.");

        // A avaliação geral é a única obrigatória — é dela que sai o índice da unidade.
        if (!respostas.TryGetValue("geral", out var geral) || string.IsNullOrWhiteSpace(geral))
            throw new ValidacaoException(
                "pesquisa.avaliacao_geral_obrigatoria", "A avaliação geral é obrigatória.");

        var limpas = respostas
            .Where(r => !string.IsNullOrWhiteSpace(r.Value))
            .ToDictionary(
                r => r.Key,
                r => r.Key == "comentario" && r.Value.Length > TamanhoMaximoComentario
                    ? r.Value[..TamanhoMaximoComentario]
                    : r.Value.Trim());

        p.RespostasJson = JsonSerializer.Serialize(limpas);
        p.RespondidaEm = DateTime.UtcNow;
        p.RespondidaIp = ip;
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/pesquisa/{token}";
    }
}
