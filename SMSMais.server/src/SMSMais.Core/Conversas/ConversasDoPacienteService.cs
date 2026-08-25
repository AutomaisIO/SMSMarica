using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Institucional;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data;

namespace SMSMais.Core.Conversas;

/// <summary>
/// Histórico de WhatsApp na aba "Conversas" do cadastro do paciente, em SESSÕES — blocos de
/// mensagens do mesmo telefone separados por 24h+ de silêncio (a janela do WhatsApp).
/// </summary>
public interface IConversasDoPacienteService
{
    /// <summary>Sessões do paciente, mais recentes primeiro.</summary>
    Task<IReadOnlyList<SessaoConversaPacienteDto>> ListarSessoesAsync(
        Guid pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Mensagens de uma sessão (o telefone + a faixa <paramref name="de"/>–<paramref name="ate"/>
    /// vêm da listagem), em ordem cronológica.
    /// </summary>
    Task<IReadOnlyList<MensagemDto>> ObterMensagensDaSessaoAsync(
        Guid pacienteId, string telefone, DateTime de, DateTime ate, CancellationToken ct = default);
}

/// <summary>
/// Deriva as sessões direto de <c>whatsapp_mensagem</c> (índice <c>(paciente_id, ocorrido_em)</c>),
/// e não de <c>conversa</c>: cobre o legado sem <c>conversa_id</c> e as mensagens de automação —
/// a ficha mostra TUDO que a SMS trocou com aquele telefone/paciente. Entram as mensagens
/// vinculadas ao paciente E as dos telefones do cadastro dele (celular de família aparece
/// marcado como "pelo telefone"). Este caminho é da FICHA (gate Pacientes.Consulta na Api) —
/// o escopo por unidade/posse da Central não se aplica aqui.
/// </summary>
public sealed class ConversasDoPacienteService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IInstituicaoService instituicao) : IConversasDoPacienteService
{
    /// <summary>Silêncio que encerra uma sessão — a mesma régua da janela do WhatsApp.</summary>
    private static readonly TimeSpan SilencioEntreSessoes = TimeSpan.FromHours(24);

    /// <summary>
    /// Teto de mensagens varridas (as MAIS RECENTES). Um paciente típico tem dezenas; o teto só
    /// protege contra telefone institucional reaproveitado. Estourou? As sessões mais antigas
    /// ficam de fora.
    /// </summary>
    private const int LimiteMensagens = 2000;

    private const int LimiteMensagensPorSessao = 500;

    public async Task<IReadOnlyList<SessaoConversaPacienteDto>> ListarSessoesAsync(
        Guid pacienteId, CancellationToken ct = default)
    {
        var paciente = await pacientes.ObterPorIdAsync(pacienteId, ct); // 404 se não existe
        var fones = await TelefonesDoCadastroAsync(paciente, ct);

        var query = db.MensagensWhatsApp.AsNoTracking();
        query = fones.Count > 0
            ? query.Where(m => m.PacienteId == pacienteId || fones.Contains(m.Telefone))
            : query.Where(m => m.PacienteId == pacienteId);

        var mensagens = await query
            .OrderByDescending(m => m.OcorridoEm)
            .Take(LimiteMensagens)
            .Select(m => new
            {
                m.Telefone,
                m.OcorridoEm,
                m.Direcao,
                m.AutorNomeExibicao,
                m.PacienteId,
            })
            .ToListAsync(ct);
        mensagens.Reverse(); // cronológica para o particionamento

        var sessoes = new List<SessaoConversaPacienteDto>();
        foreach (var grupo in mensagens.GroupBy(m => m.Telefone))
        {
            var bloco = new List<(DateTime OcorridoEm, Data.Entities.Enums.DirecaoMensagem Direcao,
                string? Autor, Guid? PacienteId)>();

            void FecharBloco()
            {
                if (bloco.Count == 0) return;
                var saidas = bloco.Where(b => b.Direcao == Data.Entities.Enums.DirecaoMensagem.Saida).ToList();
                sessoes.Add(new SessaoConversaPacienteDto(
                    grupo.Key,
                    bloco[0].OcorridoEm,
                    bloco[^1].OcorridoEm,
                    bloco.Count,
                    bloco.Count - saidas.Count,
                    saidas.Count,
                    [.. saidas.Where(s => !string.IsNullOrWhiteSpace(s.Autor))
                        .Select(s => s.Autor!).Distinct()],
                    saidas.Any(s => string.IsNullOrWhiteSpace(s.Autor)),
                    !bloco.Any(b => b.PacienteId == pacienteId)));
                bloco.Clear();
            }

            foreach (var m in grupo)
            {
                if (bloco.Count > 0 && m.OcorridoEm - bloco[^1].OcorridoEm > SilencioEntreSessoes)
                    FecharBloco();
                bloco.Add((m.OcorridoEm, m.Direcao, m.AutorNomeExibicao, m.PacienteId));
            }
            FecharBloco();
        }

        return [.. sessoes.OrderByDescending(s => s.Inicio)];
    }

    public async Task<IReadOnlyList<MensagemDto>> ObterMensagensDaSessaoAsync(
        Guid pacienteId, string telefone, DateTime de, DateTime ate, CancellationToken ct = default)
    {
        var paciente = await pacientes.ObterPorIdAsync(pacienteId, ct);
        var fones = await TelefonesDoCadastroAsync(paciente, ct);

        var fone = TelefoneWhatsApp.Canonizar(telefone ?? "");
        var foneDoCadastro = fones.Contains(fone);

        // O telefone pedido tem de pertencer ao paciente: estar no cadastro OU ter mensagem
        // vinculada a ele. Fora disso, 404 — a rota é da ficha, não um leitor livre de números.
        if (!foneDoCadastro
            && !await db.MensagensWhatsApp.AsNoTracking()
                .AnyAsync(m => m.Telefone == fone && m.PacienteId == pacienteId, ct))
            throw new NaoEncontradoException("Conversa do paciente", pacienteId);

        // Npgsql exige Kind=Utc em timestamptz; a faixa vem do próprio ListarSessoes (ISO/Z).
        var deUtc = DateTime.SpecifyKind(de.ToUniversalTime(), DateTimeKind.Utc);
        var ateUtc = DateTime.SpecifyKind(ate.ToUniversalTime(), DateTimeKind.Utc);

        var query = db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Telefone == fone && m.OcorridoEm >= deUtc && m.OcorridoEm <= ateUtc);
        // Telefone que não é do cadastro: só as mensagens VINCULADAS a este paciente — o mesmo
        // recorte que formou a sessão na listagem.
        if (!foneDoCadastro) query = query.Where(m => m.PacienteId == pacienteId);

        return await query
            .OrderBy(m => m.OcorridoEm)
            .Take(LimiteMensagensPorSessao)
            .Select(m => new MensagemDto(
                m.Id, m.ConversaId, m.Direcao, m.TipoMensagem, m.Conteudo, m.Template,
                m.AutorUsuarioId, m.AutorNomeExibicao, m.Status, m.OcorridoEm))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Telefones do cadastro em forma canônica (a chave de <c>whatsapp_mensagem.telefone</c>).
    /// Números fixos/invalidos são ignorados — WhatsApp só alcança celular.
    /// </summary>
    private async Task<List<string>> TelefonesDoCadastroAsync(PacienteDto paciente, CancellationToken ct)
    {
        var ddd = (await instituicao.ObterAsync(ct)).DddPadrao?.ToString()
            ?? TelefoneWhatsApp.DddPadraoFallback;

        // O telefone do CONTATO DE EMERGÊNCIA fica de fora de propósito: é o número de outra
        // pessoa — as conversas dela não pertencem à ficha deste paciente.
        string?[] candidatos =
            [paciente.TelefonePrincipal, paciente.TelefoneCelular, paciente.TelefoneResidencial,
             paciente.TelefoneVerificado];

        var fones = new List<string>();
        foreach (var c in candidatos)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            var interpretado = TelefoneWhatsApp.Interpretar(c, ddd);
            if (interpretado.Ok && !fones.Contains(interpretado.Fone!)) fones.Add(interpretado.Fone!);
        }
        return fones;
    }
}
