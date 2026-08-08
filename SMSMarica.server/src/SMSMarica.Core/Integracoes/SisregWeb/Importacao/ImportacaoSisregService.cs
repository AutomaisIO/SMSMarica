using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.SisregWeb.Varredura;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Importação de agendamentos do SISREG para <c>SolicitacaoExame</c>, a partir do export de
/// agendamentos (<c>expo_solicitacoes</c>) — aceita TXT (com cabeçalho de unidade) ou CSV (com
/// cabeçalho de colunas). Ver <see cref="AgendaTxtParser"/>.
///  - PREVIEW: só leitura, monta o "diff" (novo vs já existe).
///  - EXECUTAR: roda o fluxo inteiro de UMA marcação (o operador importa 1 a 1 e confere).
/// </summary>
public interface IImportacaoSisregService
{
    /// <param name="nomeArquivo">Nome do arquivo enviado (usado no CSV para achar o executante).</param>
    Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudo, string? nomeArquivo, CancellationToken ct);

    /// <summary>Importa UMA marcação (por código) do arquivo enviado. Cria paciente/unidades/solicitação.</summary>
    Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudo, string codigoSolicitacao, string? nomeArquivo, CancellationToken ct);

    /// <summary>Linhas que não viraram solicitação. <paramref name="somentePendentes"/>=false traz
    /// também as já resolvidas. <paramref name="busca"/> filtra por nome do paciente, CNS ou nº do
    /// SISREG (ignorado abaixo de 3 caracteres).</summary>
    Task<IReadOnlyList<ImportacaoFalhaDto>> ListarFalhasAsync(bool somentePendentes, string? busca, CancellationToken ct);

    /// <summary>As pendências que casam com o termo — o bloco exibido na busca de Solicitações para
    /// a recepção achar quem "não tem agendamento" (ADR-0035). Só pendentes, escopo por unidade.</summary>
    Task<IReadOnlyList<ImportacaoFalhaDto>> BuscarPendenciasPorPacienteAsync(string busca, int limite, CancellationToken ct);

    /// <summary>"Validar": reimporta a linha a partir do RAW guardado. Idempotente — se a
    /// solicitação já existir, resolve a falha em vez de duplicar. ESCRITA.</summary>
    Task<ImportacaoFalhaReprocessoResultado> ReprocessarFalhaAsync(Guid falhaId, CancellationToken ct);

    /// <summary>
    /// Pendências de SIGTAP agrupadas por procedimento — a fila de trabalho de quem vai mapear.
    /// Escopo por unidade, como o resto da aba de Erros.
    /// </summary>
    Task<IReadOnlyList<PendenciaSigtapAgrupadaDto>> ListarPendenciasSigtapAsync(CancellationToken ct);

    /// <summary>
    /// Revalida TODAS as pendências de SIGTAP de um procedimento. É o par do mapeamento: mapeia-se
    /// uma vez e as solicitações entram de uma vez. ESCRITA.
    /// </summary>
    Task<ReprocessoLoteResultado> ReprocessarPendenciasSigtapAsync(string procedimentoTexto, CancellationToken ct);

    /// <summary>"Informar CPF e importar": resolve o paciente (dedup por CPF e CNS; paciente
    /// existente nunca tem o nome alterado) e replica a linha com ele fixado. ESCRITA.</summary>
    Task<ImportacaoFalhaReprocessoResultado> ResolverComPacienteAsync(
        Guid falhaId, string? cpf, Guid? pacienteId, CancellationToken ct);

    /// <summary>Tira a linha da lista sem importar (linha inválida na origem, registro cancelado…).</summary>
    Task DescartarFalhaAsync(Guid falhaId, string? nota, CancellationToken ct);

    /// <summary>Detalhe da falha para o modal: o RAW guardado + os campos do SISREG reparseados
    /// dele (sem colunas novas no banco — o RAW é a fonte, o parser atual é a lente).</summary>
    Task<ImportacaoFalhaDetalheDto> ObterFalhaDetalheAsync(Guid falhaId, CancellationToken ct);

    /// <summary>Quem/onde, quando o serviço roda fora de uma request (lote no background).</summary>
    void DefinirContextoDeBackground(Guid? usuarioId, Guid? unidadeAtivaId);

    /// <summary>Importa UM arquivo inteiro do lote. Reconhece o arquivo antes: se não for do
    /// SISREG, descarta tudo sem tentar linha a linha.</summary>
    Task<ResultadoArquivoImportado> ImportarArquivoAsync(Guid execucaoId, string nomeArquivo, string conteudo, CancellationToken ct);

    /// <summary>
    /// Importa marcações que NÃO vieram de arquivo — a varredura da agenda (<c>cons_agendas</c>).
    /// Mesmo núcleo, mesma idempotência por nº do SISREG, mesma pendência de 1ª classe (ADR-0035):
    /// só a proveniência muda.
    ///
    /// <para>Pode ser chamado várias vezes com lotes pequenos dentro da MESMA execução — é assim
    /// que a varredura preserva o parcial quando o SISREG dispara o CAPTCHA no meio.</para>
    /// </summary>
    Task<ResultadoArquivoImportado> ImportarMarcacoesAsync(
        Guid execucaoId, IReadOnlyList<MarcacaoSisreg> marcacoes, CancellationToken ct);
}

public sealed class ImportacaoSisregService(
    SmsMaricaDbContext db,
    IConsultaCnsService consultaCns,
    IPacientesService pacientes,
    IGeradorIdentificadores geradorIds,
    IUsuarioAtualAccessor usuarioAtual,
    Varredura.Sigtap.IMapeadorSigtapSisreg mapeadorSigtap,
    ILogger<ImportacaoSisregService> logger,
    Notificacoes.Comunicacao.IComunicacaoPacienteService comunicacoes) : IImportacaoSisregService
{
    // ---- Contexto do operador ----
    // Numa request, vem do IUsuarioAtualAccessor. Num LOTE, o serviço roda no runner, onde não há
    // HttpContext: UsuarioId e UnidadeAtivaId seriam NULL — o que quebraria a resolução da unidade
    // executante (que é justamente o contexto do operador) e gravaria auditoria sem autor. Por isso
    // quem dispara o lote captura os dois na request e injeta aqui. O serviço é scoped e o runner
    // cria um escopo por lote, então esse override não vaza entre execuções.
    private Guid? _usuarioOverride;
    private Guid? _unidadeOverride;
    private bool _temOverride;

    /// <summary>Execução (arquivo) em curso — carimbada nas falhas para a aba de rastreio poder
    /// abrir "os erros desta importação". NULL fora de um lote (ex.: preview avulso).</summary>
    private Guid? _execucaoAtual;

    private Guid? UsuarioIdAtual => _temOverride ? _usuarioOverride : usuarioAtual.UsuarioId;
    private Guid? UnidadeAtivaAtual => _temOverride ? _unidadeOverride : usuarioAtual.UnidadeAtivaId;

    public void DefinirContextoDeBackground(Guid? usuarioId, Guid? unidadeAtivaId)
    {
        _usuarioOverride = usuarioId;
        _unidadeOverride = unidadeAtivaId;
        _temOverride = true;
    }

    // ===================== PREVIEW =====================

    public async Task<ImportacaoPreviewResultado> PreviewDeTextoAsync(string conteudo, string? nomeArquivo, CancellationToken ct)
    {
        var parsed = ParseArquivo(conteudo, nomeArquivo);

        // O preview é o ponto em que o arquivo inteiro passa pelo parser — é aqui que as linhas
        // ilegíveis viram falha durável (antes eram descartadas em silêncio e ninguém sabia).
        // Antes de exigir marcações: um arquivo todo quebrado é justamente o que precisa aparecer.
        await RegistrarRejeitadasAsync(parsed, nomeArquivo, ct);
        ExigirMarcacoes(parsed);

        var inicio = parsed.Cabecalho.Inicio ?? parsed.Marcacoes.Min(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        var fim = parsed.Cabecalho.Fim ?? parsed.Marcacoes.Max(m => m.DataHoraAtendimento)?.ToDateOnly() ?? default;
        // A unidade EXECUTORA é resolvida uma vez para o arquivo inteiro (é o tenant atual).
        var executante = await ResolverExecutanteAsync(parsed.Cabecalho.CnesUnidade, parsed.Cabecalho.NomeUnidade, ct);
        var preview = await MontarPreviewAsync(parsed.Marcacoes, inicio, fim, executante, ct);
        return preview with { Rejeitadas = parsed.Rejeitadas.Count };
    }

    private async Task<ImportacaoPreviewResultado> MontarPreviewAsync(
        IReadOnlyList<MarcacaoSisreg> marcacoes, DateOnly inicio, DateOnly fim, ExecutanteResolvido executante, CancellationToken ct)
    {
        var codigos = marcacoes.Select(m => m.CodigoSolicitacao).Distinct().ToList();
        var existentes = (await db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CodigoSolicitacao != null && codigos.Contains(s.CodigoSolicitacao))
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        // Existência da SOLICITANTE por CNES (o arquivo sempre traz o CNES do solicitante).
        var cnesSolic = marcacoes
            .Select(m => m.CnesUnidadeSolicitante)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var unidadesComCnes = (await db.Unidades.AsNoTracking()
            .Where(u => u.Cnes != null && cnesSolic.Contains(u.Cnes))
            .Select(u => u.Cnes!)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var sigtapComTipo = await SigtapComTipoAsync(ct);

        // Executante já resolvida (tenant atual); o mesmo nome/CNES vale para todas as linhas.
        var execNome = executante.Nome ?? marcacoes.Select(m => m.NomeUnidadeExecutante).FirstOrDefault(n => n is not null);
        var execExiste = executante.Id is not null;

        var itens = new List<ImportacaoPreviewItem>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var categoria = CategoriaSigtap.Resolver(m.CodigoSigtap);
            // "Mapeia" só faz sentido para imagem (é quem vira satélite/worklist).
            var procMapeia = categoria == CategoriaSolicitacao.Imagem
                && m.CodigoSigtap is { } sig && sigtapComTipo.Contains(sig);
            var alertas = new List<string>();
            if (executante.Erro is { } erroExec) alertas.Add(erroExec);
            if (categoria == CategoriaSolicitacao.Imagem && !procMapeia)
                alertas.Add("Exame de imagem sem tipo mapeado — importa como pendente (mapear depois).");
            else if (categoria != CategoriaSolicitacao.Imagem)
                alertas.Add($"Categoria {categoria} — importa como solicitação (sem exame de imagem/PACS).");
            if (string.IsNullOrWhiteSpace(m.CnsPaciente)) alertas.Add("Sem CNS do paciente.");
            if (m.DataHoraAtendimento is null) alertas.Add("Sem data/hora de atendimento.");

            itens.Add(new ImportacaoPreviewItem(
                m.CodigoSolicitacao, m.NomePaciente, m.CnsPaciente, m.ProcedimentoTexto, m.DataHoraAtendimento,
                m.NomeUnidadeSolicitante, m.CnesUnidadeSolicitante, execNome, executante.Cnes,
                m.NomeMedicoSolicitante,
                JaExiste: existentes.Contains(m.CodigoSolicitacao),
                UnidadeSolicitanteExiste: m.CnesUnidadeSolicitante is { } cs && unidadesComCnes.Contains(cs),
                UnidadeExecutanteExiste: execExiste,
                ProcedimentoMapeia: procMapeia,
                Alertas: alertas));
        }

        itens = [.. itens.OrderBy(i => i.JaExiste).ThenBy(i => i.DataHoraAtendimento)];
        var novos = itens.Count(i => !i.JaExiste);
        return new ImportacaoPreviewResultado(inicio, fim, itens.Count, novos, itens.Count - novos, itens, 0);
    }

    // ===================== EXECUTAR (1 registro) =====================

    public async Task<ImportacaoExecucaoResultado> ExecutarUmAsync(string conteudo, string codigoSolicitacao, string? nomeArquivo, CancellationToken ct)
    {
        var codigo = (codigoSolicitacao ?? string.Empty).Trim();
        var m = ParseOuFalhar(conteudo, nomeArquivo).Marcacoes.FirstOrDefault(x => x.CodigoSolicitacao == codigo)
            ?? throw new NaoEncontradoException("importacao.marcacao", codigo);

        var (res, jaExistia) = await ExecutarMarcacaoAsync(m, ct);

        // O que não entrou fica registrado com o RAW, para o operador corrigir a causa e revalidar.
        // "Já existe" não é erro (é a idempotência funcionando): resolve a pendência, não cria uma.
        if (res.Sucesso || jaExistia)
            await ResolverFalhaPendenteAsync(codigo, res.SolicitacaoId,
                jaExistia ? "Já existia uma solicitação com esse nº." : "Importada.", ct);
        else
            await RegistrarFalhaExecucaoAsync(m, nomeArquivo, res.Erro ?? "Erro desconhecido.",
                res.Causa ?? CausaFalhaImportacao.Outro, ct);

        return res;
    }

    /// <summary>
    /// Núcleo da importação de UMA marcação — independente de arquivo, para servir tanto ao
    /// upload quanto ao reprocessamento a partir do RAW guardado.
    /// </summary>
    /// <returns><c>jaExistia</c> distingue a idempotência (nº já importado) de uma falha real.</returns>
    /// <param name="pacienteIdForcado">
    /// Paciente já resolvido pelo operador (fluxo "informar CPF e importar" — ADR-0035). Quando
    /// preenchido, o bloco CNS→CADSUS inteiro é pulado; todo o resto do fluxo é idêntico, de
    /// propósito: é um caminho privilegiado, não um caminho paralelo.
    /// </param>
    private async Task<(ImportacaoExecucaoResultado Resultado, bool JaExistia)> ExecutarMarcacaoAsync(
        MarcacaoSisreg m, CancellationToken ct, Guid? pacienteIdForcado = null)
    {
        var codigo = m.CodigoSolicitacao;
        var passos = new List<string>();
        ImportacaoExecucaoResultado Falha(string erro, CausaFalhaImportacao causa) =>
            new(codigo, false, null, null, null, false, false, false, passos, erro, causa);

        // 1. Idempotência (nº SISREG).
        if (await db.Solicitacoes.AsNoTracking().AnyAsync(
                s => s.CodigoSolicitacao == codigo && s.ExcluidoEm == null, ct))
            return (Falha("Já existe uma solicitação com esse número do SISREG.", CausaFalhaImportacao.Outro), true);

        // 2. Natureza pelo subgrupo SIGTAP (roteia satélite/UI). SÓ imagem precisa de TipoExame —
        //    e mesmo SEM tipo mapeado a importação NÃO trava: entra como pendente (ADR-0021).
        var sig = SoDigitos(m.CodigoSigtap);
        var categoria = CategoriaSigtap.Resolver(sig);
        Guid? tipoExameId = null;
        if (categoria == CategoriaSolicitacao.Imagem)
        {
            var tipos = await db.TiposExame.AsNoTracking()
                .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
                .Select(t => new { t.Id, Codigo = t.ProcedimentoSigtap!.Codigo })
                .ToListAsync(ct);
            tipoExameId = tipos.FirstOrDefault(t => SoDigitos(t.Codigo) == sig)?.Id;

            // Sem tipo mapeado, a tela "Mapeamento pendente" mostra o exame — mas só resolve se
            // houver um TipoExame para vincular. E criar TipoExame exige um procedimento no
            // catálogo SIGTAP (FK obrigatória). Se o código nem estiver catalogado, o operador vê
            // a pendência e não tem como sair dela: beco sem saída.
            if (tipoExameId is null) await CatalogarSigtapSeNovoAsync(sig, m.ProcedimentoTexto, ct);

            passos.Add(tipoExameId is null
                ? $"Exame de imagem \"{m.ProcedimentoTexto}\" (SIGTAP {sig}) — SEM tipo mapeado; importa como PENDENTE."
                : $"Exame de imagem \"{m.ProcedimentoTexto}\" → tipo mapeado (SIGTAP {sig}).");
        }
        else
        {
            passos.Add($"Categoria {categoria} (SIGTAP {sig}) — importa como solicitação, sem satélite de execução.");
        }

        // 3. Paciente. PRIMEIRO tenta a NOSSA base por CNS — o CADSUS/SISREG tem limite de
        //    500 req/hora; um lote grande estoura e passa a falhar TUDO como "não encontrado".
        //    A maioria dos pacientes já existe, então só quem falta de fato consulta o SISREG.
        Guid pacienteId;
        bool pacienteCriado;
        string? nomeResolvido;

        // Caminho privilegiado: o operador já identificou o paciente na tela de pendências
        // (ADR-0035). Não há CNS a consultar nem CADSUS a chamar — a identidade já foi decidida
        // por uma pessoa, que é uma fonte melhor que o cadweb50.
        if (pacienteIdForcado is { } forcado)
        {
            // ObterPorIdAsync lança NaoEncontrado se sumiu — que é a resposta honesta: o operador
            // escolheu um paciente que não existe mais, e isso não é uma falha de importação.
            var informado = await pacientes.ObterPorIdAsync(forcado, ct);
            pacienteId = informado.Id;
            pacienteCriado = false;
            nomeResolvido = informado.NomeCompleto;
            passos.Add($"Paciente informado pelo operador: {informado.NomeCompleto}.");
        }
        else if (string.IsNullOrWhiteSpace(m.CnsPaciente))
        {
            return (Falha("Marcação sem CNS do paciente.", CausaFalhaImportacao.SemCns), false);
        }
        else if (await pacientes.ObterPorCnsAsync(m.CnsPaciente!, ct) is { } porCns)
        {
            pacienteId = porCns.Id;
            pacienteCriado = false;
            nomeResolvido = porCns.NomeCompleto;
            passos.Add($"Paciente já cadastrado (CNS {Mascara(m.CnsPaciente)}) — reusa, sem consultar o SISREG.");
        }
        else
        {
            // Só agora vai ao CADSUS (CNS → CPF + demografia) — o passo caro/limitado.
            ConsultaCnsRespostaDto cadsus;
            try { cadsus = await consultaCns.ConsultarPorCnsAsync(m.CnsPaciente!, ct); }
            catch (Exception ex) { return (Falha($"Falha ao consultar o paciente no SISREG (CNS): {ex.Message}", CausaFalhaImportacao.CadsusIndisponivel), false); }
            passos.Add($"CNS {Mascara(m.CnsPaciente)} → CPF {Mascara(cadsus.Cpf)} (cadweb50).");

            // Pode já existir por CPF (mesmo cidadão cadastrado sob outro CNS). Nunca altera o nome.
            var porCpf = await pacientes.ObterPorCpfAsync(cadsus.Cpf, ct);
            if (porCpf is not null)
            {
                pacienteId = porCpf.Id;
                pacienteCriado = false;
                nomeResolvido = porCpf.NomeCompleto;
                passos.Add("Paciente já cadastrado (por CPF) — reusa, sem alterar o nome.");
            }
            else
            {
                // Paciente inexistente E sem CPF do CADSUS: não dá para cadastrar com segurança
                // (sem CPF não há identidade). Cai em falha honesta em vez do falso "CPF duplicado".
                if (SoDigitos(cadsus.Cpf).Length != 11)
                    return (Falha(
                        "O CADSUS não retornou o CPF deste CNS e o paciente ainda não existe no sistema. Informe o CPF nesta pendência para importar.",
                        CausaFalhaImportacao.CpfNaoResolvido), false);

                // Telefone do TXT vai num slot NÃO-principal (celular se móvel, senão residencial) —
                // o principal é o contato validado por OTP e é intocável pela automação (ADR-0020).
                var (celular, residencial) = MontarTelefoneDoTxt(m.TelefonePaciente);
                // Endereço só entra no CREATE (paciente novo); paciente existente nunca é sobrescrito.
                var endereco = MontarEnderecoDoTxt(m);

                var nomeNovo = (cadsus.Nome.Length > 0 ? cadsus.Nome : m.NomePaciente) ?? "SEM NOME";
                pacienteId = await pacientes.CadastrarAsync(new CadastrarPacienteRequest(
                    NomeCompleto: nomeNovo,
                    Cpf: cadsus.Cpf,
                    DataNascimento: cadsus.DataNascimento ?? default,
                    Cns: cadsus.Cns,
                    Rg: null,
                    Sexo: cadsus.Sexo == "Masculino" ? Sexo.Masculino : cadsus.Sexo == "Feminino" ? Sexo.Feminino : Sexo.NaoInformado,
                    NomeDaMae: cadsus.NomeMae,
                    Endereco: endereco,
                    TelefoneCelular: celular,
                    TelefoneResidencial: residencial), ct);
                pacienteCriado = true;
                nomeResolvido = nomeNovo;
                passos.Add("Paciente novo → criado a partir do CADSUS (+ telefone/endereço do TXT).");
            }
        }

        // 4. Unidades. EXECUTORA = o tenant atual (contexto da unidade em que o operador importa) —
        //    atribuição explícita, resolvida uma vez para o arquivo. SOLICITANTE = por CNES do
        //    arquivo (cria se ainda não existir).
        var exec = await ResolverExecutanteAsync(m.CnesUnidadeExecutante, m.NomeUnidadeExecutante, ct);
        if (exec.Id is null)
            return (Falha(exec.Erro ?? "Não identifiquei a unidade executante.", CausaFalhaImportacao.UnidadeNaoResolvida), false);
        var unidadeExecId = exec.Id.Value;
        var execCriada = exec.Criada;
        passos.Add(execCriada
            ? $"Unidade executante criada do cabeçalho: {exec.Nome}{CnesSufixo(exec.Cnes)}."
            : $"Unidade executante: {exec.Nome} (contexto atual).");

        var (unidadeSolicId, solicCriada) = await ResolverOuCriarUnidadeAsync(m.CnesUnidadeSolicitante, m.NomeUnidadeSolicitante, ct);
        if (solicCriada) passos.Add($"Unidade solicitante criada: {m.NomeUnidadeSolicitante}{CnesSufixo(m.CnesUnidadeSolicitante)}.");
        else if (unidadeSolicId is not null) passos.Add("Unidade solicitante já cadastrada.");

        // 5. Espinha de regulação (sempre) + satélite de execução SÓ para imagem. Ver ADR-0021.
        var agora = DateTime.UtcNow;
        var solic = new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Categoria = categoria,
            ProcedimentoCodigoSisreg = m.CodigoProcedimentoSisreg,
            ProcedimentoSigtapCodigo = m.CodigoSigtap,
            ProcedimentoTexto = m.ProcedimentoTexto,
            // Consulta colapsa no SIGTAP 0301010072 — a especialidade só existe no texto.
            EspecialidadeTexto = categoria == CategoriaSolicitacao.Consulta ? m.ProcedimentoTexto : null,
            UnidadeExecutanteId = unidadeExecId,
            UnidadeSolicitanteId = unidadeSolicId,
            SolicitanteNome = m.NomeMedicoSolicitante ?? "NÃO INFORMADO",
            SolicitanteNumConselho = string.Empty,
            SolicitanteUfConselho = string.Empty,
            SolicitanteConselho = "CRM",
            SolicitanteCpf = m.CpfMedicoSolicitante,
            RawSisreg = m.LinhaRaw,
            CodigoSolicitacao = codigo,
            Status = StatusSolicitacao.Solicitada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            // O SISREG entrega hora LOCAL de Brasília (GMT-3) → UTC (+3h).
            DataAgendada = m.DataHoraAtendimento is { } dh ? ParaUtcBrasilia(dh) : null,
            DataSolicitacao = m.DataSolicitacao,
            DataRegulacao = m.DataRegulacao,
            CriadoEm = agora,
            CriadoPor = UsuarioIdAtual,
        };
        db.Solicitacoes.Add(solic);

        // AccessionNumber/StudyUID são conceitos DICOM — só existem no satélite de imagem.
        Guid idPublico = solic.Id;
        string? accession = null;
        if (categoria == CategoriaSolicitacao.Imagem)
        {
            accession = await geradorIds.ProximoAccessionAsync(ct);
            var exame = new ExameImagem
            {
                Id = Guid.CreateVersion7(),
                Solicitacao = solic,
                AccessionNumber = accession,
                StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
                TipoExameId = tipoExameId, // nullable = mapeamento pendente
                Status = StatusSolicitacaoExame.Solicitada,
                // NADA vai ao PACS automaticamente: só quando a recepção AUTORIZA.
                ProximaTentativaEm = null,
                CriadoEm = agora,
                CriadoPor = UsuarioIdAtual,
            };
            db.ExamesImagem.Add(exame);
            idPublico = exame.Id;
        }

        // Notificação WhatsApp de confirmação — só enfileira (o worker envia com ritmo).
        // Dois gates, em "E": o da unidade executante e o do procedimento. Ambos nascem ligados,
        // e ausência de configuração significa ENVIAR — silenciar o paciente por causa de uma
        // lacuna de cadastro seria pior do que uma mensagem a mais.
        if (await DeveEnviarConfirmacaoAsync(unidadeExecId, m, ct))
        {
            await comunicacoes.EnfileirarAsync(solic, Data.Entities.Enums.FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        }
        else
        {
            passos.Add("Confirmação por WhatsApp não enviada (desligada para esta unidade ou procedimento).");
        }

        await db.SaveChangesAsync(ct);
        passos.Add(accession is null ? "Solicitação criada." : $"Solicitação criada (accession {accession}).");

        return (new ImportacaoExecucaoResultado(
            codigo, true, idPublico, accession ?? string.Empty,
            nomeResolvido, pacienteCriado, solicCriada, execCriada, passos, null), false);
    }

    // ===================== LOTE (um arquivo) =====================

    public async Task<ResultadoArquivoImportado> ImportarArquivoAsync(
        Guid execucaoId, string nomeArquivo, string conteudo, CancellationToken ct)
    {
        _execucaoAtual = execucaoId;

        // 1. É mesmo um export do SISREG? Contagem de coluna é teste fraco — a assinatura olha a
        //    FORMA da primeira linha de dados. Reprovou: descarta o arquivo INTEIRO sem tentar
        //    linha a linha, senão um CSV alheio viraria centenas de falhas de lixo na aba Erros.
        var assinatura = AgendaTxtParser.Reconhecer(conteudo);
        if (!assinatura.Reconhecido)
        {
            await RegistrarArquivoIncompativelAsync(execucaoId, nomeArquivo, conteudo, assinatura.Motivo, ct);
            return new ResultadoArquivoImportado(0, 0, 0, 0, true, assinatura.Motivo);
        }

        var parsed = AgendaTxtParser.Parse(conteudo, nomeArquivo);
        await RegistrarRejeitadasAsync(parsed, nomeArquivo, ct);

        // Linha que o parser recusou já nasce inválida (e já está na aba Erros).
        var invalidos = parsed.Rejeitadas.Count;
        var validos = 0;
        var jaExistiam = 0;

        foreach (var m in parsed.Marcacoes)
        {
            ct.ThrowIfCancellationRequested();

            var (res, jaExistia) = await ExecutarMarcacaoAsync(m, ct);
            if (res.Sucesso || jaExistia)
            {
                // "Já existia" conta como válido: o arquivo está honrado, não há nada a corrigir.
                validos++;
                if (jaExistia) jaExistiam++;
                await ResolverFalhaPendenteAsync(m.CodigoSolicitacao, res.SolicitacaoId,
                    jaExistia ? "Já existia uma solicitação com esse nº." : "Importada.", ct);
            }
            else
            {
                invalidos++;
                await RegistrarFalhaExecucaoAsync(m, nomeArquivo, res.Erro ?? "Erro desconhecido.",
                    res.Causa ?? CausaFalhaImportacao.Outro, ct);
            }
        }

        return new ResultadoArquivoImportado(
            parsed.Marcacoes.Count + parsed.Rejeitadas.Count, validos, invalidos, jaExistiam, false, null);
    }

    public async Task<ResultadoArquivoImportado> ImportarMarcacoesAsync(
        Guid execucaoId, IReadOnlyList<MarcacaoSisreg> marcacoes, CancellationToken ct)
    {
        _execucaoAtual = execucaoId;

        var validos = 0;
        var invalidos = 0;
        var jaExistiam = 0;

        foreach (var m in marcacoes)
        {
            ct.ThrowIfCancellationRequested();

            // Gate do SIGTAP. Sem ele, ExecutarMarcacaoAsync criaria a solicitação com categoria
            // "Outro", sem satélite de imagem, sem worklist — e marcada como SUCESSO. Lixo
            // silencioso em escala de centenas por dia, que nenhuma tela mostraria. Vira pendência
            // acionável: o operador confirma o de-para uma vez e revalida.
            if (string.IsNullOrWhiteSpace(m.CodigoSigtap))
            {
                invalidos++;
                await RegistrarFalhaExecucaoAsync(
                    m, nomeArquivo: null,
                    $"O procedimento \"{m.ProcedimentoTexto ?? m.CodigoProcedimentoSisreg}\" não tem "
                    + "código SIGTAP: o nome não bate exatamente com nenhum procedimento do catálogo. "
                    + "Mapeie-o para liberar todas as solicitações deste procedimento.",
                    CausaFalhaImportacao.SigtapNaoMapeado, ct, OrigemFalhaImportacao.Varredura);
                continue;
            }

            var (res, jaExistia) = await ExecutarMarcacaoAsync(m, ct);
            if (res.Sucesso || jaExistia)
            {
                validos++;
                if (jaExistia) jaExistiam++;
                await ResolverFalhaPendenteAsync(m.CodigoSolicitacao, res.SolicitacaoId,
                    jaExistia ? "Já existia uma solicitação com esse nº." : "Importada pela varredura da agenda.", ct);
            }
            else
            {
                invalidos++;
                await RegistrarFalhaExecucaoAsync(m, nomeArquivo: null, res.Erro ?? "Erro desconhecido.",
                    res.Causa ?? CausaFalhaImportacao.Outro, ct, OrigemFalhaImportacao.Varredura);
            }
        }

        return new ResultadoArquivoImportado(marcacoes.Count, validos, invalidos, jaExistiam, false, null);
    }

    /// <summary>Arquivo .txt/.csv que não é do SISREG: uma falha só, do arquivo — não uma por linha.</summary>
    private async Task RegistrarArquivoIncompativelAsync(
        Guid execucaoId, string nomeArquivo, string conteudo, string motivo, CancellationToken ct)
    {
        // Guarda só um trecho: o RAW aqui serve para o operador reconhecer o que mandou, não para
        // revalidar (não há linha do SISREG). Arquivo inteiro no banco seria desperdício.
        var trecho = Truncar(conteudo.Replace("\r\n", "\n").Trim(), 2000);
        var hash = Sha256($"{nomeArquivo}|{trecho}");

        var f = await db.SisregImportacaoFalhas.FirstOrDefaultAsync(
            x => x.CodigoSolicitacao == null && x.HashLinha == hash && x.ResolvidoEm == null, ct);

        var agora = DateTime.UtcNow;
        if (f is null)
        {
            f = new SisregImportacaoFalha { Id = Guid.CreateVersion7(), CriadoEm = agora, HashLinha = hash };
            db.SisregImportacaoFalhas.Add(f);
        }

        f.CodigoSolicitacao = null;
        f.LinhaRaw = trecho;
        f.Origem = OrigemFalhaImportacao.Arquivo;
        f.Causa = CausaFalhaImportacao.ArquivoIncompativel;
        f.Motivo = Truncar($"Arquivo incompatível: {motivo}", 2000);
        f.NomeArquivo = Truncar(nomeArquivo, 300);
        f.ExecucaoId = execucaoId;
        f.UnidadeExecutanteId = UnidadeAtivaAtual;
        f.Tentativas++;
        f.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);
    }

    // ===================== FALHAS (lista + validar) =====================

    public async Task<IReadOnlyList<ImportacaoFalhaDto>> ListarFalhasAsync(
        bool somentePendentes, string? busca, CancellationToken ct)
    {
        var q = db.SisregImportacaoFalhas.AsNoTracking();
        if (somentePendentes) q = q.Where(f => f.ResolvidoEm == null);
        // Multitenancy: o operador vê as falhas da unidade em que está importando. Sem contexto
        // (admin global) vê tudo — mesma régua da listagem de solicitações.
        if (UnidadeAtivaAtual is { } uid) q = q.Where(f => f.UnidadeExecutanteId == uid);
        q = AplicarBuscaDeFalha(q, busca);

        return await ProjetarFalhas(q.OrderByDescending(f => f.AtualizadoEm)).ToListAsync(ct);
    }

    /// <summary>
    /// Busca da pendência por paciente — nome, CNS ou nº do SISREG. É o que faz a recepção achar a
    /// pessoa que chegou e "não tem agendamento" (ADR-0035). Termo curto demais não filtra nada:
    /// devolveria a lista inteira disfarçada de resultado de busca.
    /// </summary>
    private static IQueryable<SisregImportacaoFalha> AplicarBuscaDeFalha(
        IQueryable<SisregImportacaoFalha> q, string? busca)
    {
        var termo = busca?.Trim();
        if (string.IsNullOrEmpty(termo) || termo.Length < 3) return q;

        var padrao = $"%{termo}%";
        var digitos = SoDigitos(termo);
        // A condição sobre o TAMANHO é decidida aqui, em C#: dentro da expressão o EF a traduziria
        // para SQL, e um termo sem dígitos viraria LIKE '%%' — a lista inteira disfarçada de busca.
        var comDigitos = digitos.Length >= 3;

        return q.Where(f =>
            (f.NomePaciente != null && EF.Functions.ILike(f.NomePaciente, padrao))
            || (comDigitos && f.PacienteCns != null && f.PacienteCns.Contains(digitos))
            || (comDigitos && f.CodigoSolicitacao != null && f.CodigoSolicitacao.Contains(digitos)));
    }

    private static IQueryable<ImportacaoFalhaDto> ProjetarFalhas(IQueryable<SisregImportacaoFalha> q) =>
        q.Select(f => new ImportacaoFalhaDto(
            f.Id, f.CodigoSolicitacao, f.Origem, f.Motivo, f.LinhaRaw, f.NomeArquivo,
            f.NomePaciente, f.ProcedimentoTexto, f.DataAgendada, f.NomeExecutante,
            f.Tentativas, f.CriadoEm, f.AtualizadoEm, f.ResolvidoEm, f.ResolucaoNota, f.SolicitacaoId,
            f.Causa, f.PacienteCns));

    /// <summary>
    /// As pendências que casam com o termo — o bloco que a busca de Solicitações mostra acima da
    /// lista. Escopo por unidade executante, só pendentes, teto baixo: é um aviso, não uma listagem.
    /// </summary>
    public async Task<IReadOnlyList<ImportacaoFalhaDto>> BuscarPendenciasPorPacienteAsync(
        string busca, int limite, CancellationToken ct)
    {
        var termo = busca?.Trim();
        if (string.IsNullOrEmpty(termo) || termo.Length < 3) return [];

        var q = db.SisregImportacaoFalhas.AsNoTracking().Where(f => f.ResolvidoEm == null);
        if (UnidadeAtivaAtual is { } uid) q = q.Where(f => f.UnidadeExecutanteId == uid);

        return await ProjetarFalhas(AplicarBuscaDeFalha(q, termo).OrderByDescending(f => f.DataAgendada))
            .Take(limite)
            .ToListAsync(ct);
    }

    public async Task<ImportacaoFalhaDetalheDto> ObterFalhaDetalheAsync(Guid falhaId, CancellationToken ct)
    {
        var f = await db.SisregImportacaoFalhas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == falhaId, ct)
            ?? throw new NaoEncontradoException("importacao.falha", falhaId.ToString());

        var dto = new ImportacaoFalhaDto(
            f.Id, f.CodigoSolicitacao, f.Origem, f.Motivo, f.LinhaRaw, f.NomeArquivo, f.NomePaciente,
            f.ProcedimentoTexto, f.DataAgendada, f.NomeExecutante, f.Tentativas, f.CriadoEm,
            f.AtualizadoEm, f.ResolvidoEm, f.ResolucaoNota, f.SolicitacaoId, f.Causa, f.PacienteCns);

        // Arquivo incompatível não tem linha do SISREG pra parsear — o RAW é um trecho do arquivo.
        // Degrada pro que existe, em vez de fingir campos.
        if (f.Origem == OrigemFalhaImportacao.Arquivo)
            return new(dto, false, [], null, null, null, f.CnesExecutante);

        // A varredura da agenda também produz linha de TXT (ela exporta o mesmo arquivo do
        // expo_solicitacoes), então não há caminho separado: o RAW é sempre uma linha do SISREG e
        // o parser abaixo serve às duas origens.
        var parsed = AgendaTxtParser.Parse(ReconstruirConteudo(f), f.NomeArquivo);
        var m = parsed.Marcacoes.FirstOrDefault();
        if (m is null)
            return new(dto, false, [], null, null, f.NomeExecutante, f.CnesExecutante);

        var campos = new List<CampoSisreg>
        {
            new(0, "Nº da solicitação", m.CodigoSolicitacao),
            new(2, "Código SIGTAP", m.CodigoSigtap),
            new(3, "Procedimento", m.ProcedimentoTexto),
            new(6, "Data/hora do atendimento", m.DataHoraAtendimento?.ToString("dd/MM/yyyy HH:mm")),
            new(9, "CNS do paciente", m.CnsPaciente),
            new(10, "Paciente", m.NomePaciente),
            new(21, "Telefone", m.TelefonePaciente),
            new(15, "Endereço", MontarEnderecoTexto(m)),
            new(20, "CEP", m.Cep),
            new(22, "Município de residência", m.MunicipioResidencia),
            new(26, "CNES do solicitante", m.CnesUnidadeSolicitante),
            new(27, "Unidade solicitante", m.NomeUnidadeSolicitante),
            new(29, "Data da solicitação", m.DataSolicitacao?.ToString("dd/MM/yyyy")),
            new(31, "Data da regulação", m.DataRegulacao?.ToString("dd/MM/yyyy")),
            new(35, "CID", m.Cid),
            new(36, "CPF do solicitante", m.CpfMedicoSolicitante),
            new(37, "Médico solicitante", m.NomeMedicoSolicitante),
        };

        return new(dto, true, campos,
            m.NomeUnidadeSolicitante, m.CnesUnidadeSolicitante,
            m.NomeUnidadeExecutante ?? f.NomeExecutante, m.CnesUnidadeExecutante ?? f.CnesExecutante);
    }

    private static string? MontarEnderecoTexto(MarcacaoSisreg m)
    {
        var partes = new[] { m.TipoLogradouro, m.Logradouro, m.Numero, m.Complemento, m.Bairro }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var s = string.Join(' ', partes).Trim();
        return s.Length == 0 ? null : s;
    }

    public Task<ImportacaoFalhaReprocessoResultado> ReprocessarFalhaAsync(Guid falhaId, CancellationToken ct) =>
        ReplicarFalhaAsync(falhaId, pacienteIdForcado: null, ct);

    /// <summary>
    /// "Informar CPF e importar" (ADR-0035). Resolve o paciente — respeitando a régua de dedup do hub
    /// (procura por CPF **e** CNS antes de criar; paciente existente NUNCA tem o nome alterado) — e
    /// então replica a linha com esse paciente fixado.
    /// </summary>
    public async Task<ImportacaoFalhaReprocessoResultado> ResolverComPacienteAsync(
        Guid falhaId, string? cpf, Guid? pacienteId, CancellationToken ct)
    {
        var f = await db.SisregImportacaoFalhas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == falhaId, ct)
            ?? throw new NaoEncontradoException("importacao.falha", falhaId.ToString());
        if (f.ResolvidoEm is not null)
            throw new ConflitoException("falha.ja_resolvida", "Esta pendência já foi resolvida por outro operador.");

        var resolvido = pacienteId ?? await ResolverPacienteInformadoAsync(f, cpf, ct);
        return await ReplicarFalhaAsync(falhaId, resolvido, ct);
    }

    /// <summary>
    /// CPF informado → paciente EXISTENTE. A ordem é a regra de dedup do hub, não deste fluxo:
    /// procura por CPF, depois pelo CNS que a própria pendência carimbou (o mesmo cidadão pode já
    /// estar cadastrado sob outra chave). Reusar sem procurar é como se fabricam as duplicatas que
    /// partem o histórico clínico em dois.
    ///
    /// Não encontrando, <b>não cadastra</b>: o export do SISREG não traz data de nascimento, e criar
    /// um Patient no hub com nascimento default gravaria lixo permanente na identidade do cidadão —
    /// exatamente o que o ADR-0035 quis evitar ao recusar o "paciente provisório". O operador
    /// cadastra na tela de Pacientes (onde os campos obrigatórios são cobrados) e volta aqui.
    /// </summary>
    private async Task<Guid> ResolverPacienteInformadoAsync(
        SisregImportacaoFalha f, string? cpf, CancellationToken ct)
    {
        var digitos = SoDigitos(cpf);
        if (digitos.Length != 11)
            throw new ValidacaoException("falha.cpf_invalido", "Informe um CPF válido (11 dígitos).");

        if (await pacientes.ObterPorCpfAsync(digitos, ct) is { } porCpf) return porCpf.Id;

        // O CNS da pendência é a segunda chance de achar o mesmo cidadão sob outro cadastro.
        if (!string.IsNullOrWhiteSpace(f.PacienteCns)
            && await pacientes.ObterPorCnsAsync(f.PacienteCns!, ct) is { } porCns)
            return porCns.Id;

        throw new ValidacaoException("falha.paciente_nao_cadastrado",
            $"Não há paciente cadastrado com o CPF {digitos}. Cadastre-o em Pacientes (o SISREG não "
            + "informa a data de nascimento, então o cadastro não pode ser feito por aqui) e depois "
            + "volte para importar esta pendência.");
    }

    /// <summary>
    /// O replay da linha a partir do RAW guardado — único caminho, com ou sem paciente informado.
    /// Idempotente por nº do SISREG: revalidar algo que já foi criado resolve a pendência em vez de
    /// duplicar. Falha por outro motivo mantém a pendência ABERTA, com tentativa e causa atualizadas.
    /// </summary>
    private async Task<ImportacaoFalhaReprocessoResultado> ReplicarFalhaAsync(
        Guid falhaId, Guid? pacienteIdForcado, CancellationToken ct)
    {
        var f = await db.SisregImportacaoFalhas.FirstOrDefaultAsync(x => x.Id == falhaId, ct)
            ?? throw new NaoEncontradoException("importacao.falha", falhaId.ToString());
        if (f.ResolvidoEm is not null)
            return new(f.Id, true, null, "Esta linha já estava resolvida.");

        // Arquivo incompatível não se revalida: o RAW aqui é um trecho do arquivo, não uma linha do
        // SISREG. Não há o que reprocessar — a correção é enviar o arquivo certo. Sem esta guarda,
        // cairia no parser e devolveria "a linha continua ilegível", que confunde o operador.
        if (f.Origem == OrigemFalhaImportacao.Arquivo)
            return new(f.Id, false, null,
                "Este registro é um arquivo inteiro que não é do SISREG — não há linha para revalidar. Envie o arquivo correto, ou descarte este registro.");

        var agora = DateTime.UtcNow;
        f.Tentativas++;
        f.AtualizadoEm = agora;

        // Reconstrói o mínimo de "arquivo" que o parser precisa (cabeçalho + a linha) — assim o
        // reprocesso usa exatamente o mesmo parser da importação, sem caminho paralelo. Vale para
        // as duas origens: a varredura também produz linha do mesmo TXT.
        var parsed = AgendaTxtParser.Parse(ReconstruirConteudo(f), f.NomeArquivo);
        var m = parsed.Marcacoes.FirstOrDefault();
        if (m is null)
        {
            var motivo = parsed.Rejeitadas.FirstOrDefault()?.Motivo ?? "A linha continua ilegível para o parser.";
            f.Origem = OrigemFalhaImportacao.Parser;
            f.Causa = CausaFalhaImportacao.LinhaInvalida;
            f.Motivo = Truncar(motivo, 2000);
            await db.SaveChangesAsync(ct);
            return new(f.Id, false, null,
                $"A linha continua inválida: {motivo} Corrija na origem e reimporte o arquivo, ou descarte esta linha.");
        }

        var (res, jaExistia) = await ExecutarMarcacaoAsync(m, ct, pacienteIdForcado);

        // A linha pôde ser lida agora: carimba o nº que faltava (caso de falha do parser).
        f.CodigoSolicitacao = m.CodigoSolicitacao;
        f.NomePaciente = Truncar(m.NomePaciente, 300);
        f.ProcedimentoTexto = Truncar(m.ProcedimentoTexto, 500);
        f.PacienteCns ??= Truncar(SoDigitos(m.CnsPaciente) is { Length: > 0 } cns ? cns : null, 15);
        f.DataAgendada = m.DataHoraAtendimento is { } dh ? ParaUtcBrasilia(dh) : null;

        if (res.Sucesso || jaExistia)
        {
            // O ponto do ticket: revalidar algo que já foi criado NÃO duplica — dá ok e sai da lista.
            f.ResolvidoEm = agora;
            f.ResolvidoPor = UsuarioIdAtual;
            f.ResolucaoNota = jaExistia
                ? "Já existia uma solicitação com esse nº."
                : pacienteIdForcado is null ? "Importada na validação." : "Importada com o CPF informado pelo operador.";
            f.SolicitacaoId = res.SolicitacaoId;
            await db.SaveChangesAsync(ct);
            return new(f.Id, true, res, jaExistia
                ? "Esta marcação já está no sistema — a linha saiu da lista de erros."
                : "Importada com sucesso — a linha saiu da lista de erros.");
        }

        // Preserva a Varredura: a origem é o que decide COMO ler o RAW no próximo reprocesso.
        // Sobrescrever com Execucao mandaria a próxima validação parsear um envelope JSON com o
        // parser de TXT — e a pendência ficaria presa para sempre em "linha ilegível".
        if (f.Origem != OrigemFalhaImportacao.Varredura)
            f.Origem = OrigemFalhaImportacao.Execucao;

        f.Causa = res.Causa ?? CausaFalhaImportacao.Outro;
        f.Motivo = Truncar(res.Erro ?? "Erro desconhecido.", 2000);
        await db.SaveChangesAsync(ct);
        return new(f.Id, false, res, res.Erro ?? "Ainda não foi possível importar esta linha.");
    }

    public async Task<IReadOnlyList<PendenciaSigtapAgrupadaDto>> ListarPendenciasSigtapAsync(CancellationToken ct)
    {
        var unidade = UnidadeAtivaAtual;

        var pendentes = await db.SisregImportacaoFalhas.AsNoTracking()
            .Where(f => f.ResolvidoEm == null
                        && f.Causa == CausaFalhaImportacao.SigtapNaoMapeado
                        && (unidade == null || f.UnidadeExecutanteId == unidade))
            .Select(f => new { f.ProcedimentoTexto, f.CriadoEm, f.LinhaRaw })
            .ToListAsync(ct);

        var grupos = pendentes
            .Where(x => !string.IsNullOrWhiteSpace(x.ProcedimentoTexto))
            .GroupBy(x => x.ProcedimentoTexto!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Texto = g.Key,
                // O `pa` sai do envelope; pode divergir dentro do grupo quando a varredura passou
                // pelo "GRUPO -" e pelo item individual. O primeiro serve de referência.
                Codigo = g.Select(x => CodigoDoRaw(x.LinhaRaw)).FirstOrDefault(c => c is not null),
                Qtd = g.Count(),
                Primeira = g.Min(x => x.CriadoEm),
                Ultima = g.Max(x => x.CriadoEm),
            })
            .ToList();

        var catalogo = await mapeadorSigtap.ObterCatalogoAsync(
            [.. grupos.Select(g => g.Codigo).Where(c => c is not null).Distinct(StringComparer.Ordinal)!], ct);

        return [.. grupos
            .Select(g => new PendenciaSigtapAgrupadaDto(
                g.Texto,
                g.Codigo,
                g.Codigo is not null && catalogo.TryGetValue(g.Codigo, out var info) ? info.DeParaId : null,
                g.Qtd,
                g.Primeira,
                g.Ultima))
            .OrderByDescending(x => x.Solicitacoes)
            .ThenBy(x => x.ProcedimentoTexto, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<ReprocessoLoteResultado> ReprocessarPendenciasSigtapAsync(
        string procedimentoTexto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(procedimentoTexto))
            throw new ValidacaoException("importacao.procedimento_obrigatorio", "Informe o procedimento.");

        var unidade = UnidadeAtivaAtual;

        var ids = await db.SisregImportacaoFalhas.AsNoTracking()
            .Where(f => f.ResolvidoEm == null
                        && f.Causa == CausaFalhaImportacao.SigtapNaoMapeado
                        && f.ProcedimentoTexto == procedimentoTexto
                        && (unidade == null || f.UnidadeExecutanteId == unidade))
            .Select(f => f.Id)
            .ToListAsync(ct);

        var importadas = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            // Reusa o "Validar" de uma pendência só — mesmo caminho, mesma idempotência. Uma linha
            // que falhe por OUTRA causa (paciente sem CNS) segue pendente com a causa nova, e é
            // isso que se quer: o lote resolve o SIGTAP, não varre problema para debaixo do tapete.
            var r = await ReprocessarFalhaAsync(id, ct);
            if (r.Resolvida) importadas++;
        }

        var continuam = ids.Count - importadas;
        var mensagem = ids.Count == 0
            ? "Não havia pendências deste procedimento."
            : continuam == 0
                ? $"{importadas} solicitações importadas."
                : $"{importadas} importadas; {continuam} continuam pendentes por outro motivo "
                  + "(veja a lista de erros).";

        return new ReprocessoLoteResultado(ids.Count, importadas, continuam, mensagem);
    }

    /// <summary>
    /// Código do procedimento no SISREG (o <c>pa</c>) — coluna 1 da linha do TXT. O parser não o
    /// carrega para <see cref="MarcacaoSisreg"/> (ele usa o SIGTAP da coluna 2), mas a tela de
    /// pendências agrupadas precisa dele para apontar qual procedimento mapear.
    /// </summary>
    private static string? CodigoDoRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var campos = raw.Split(';');
        return campos.Length > 1 && campos[1].Trim() is { Length: > 0 } c ? c : null;
    }

    public async Task DescartarFalhaAsync(Guid falhaId, string? nota, CancellationToken ct)
    {
        var f = await db.SisregImportacaoFalhas.FirstOrDefaultAsync(x => x.Id == falhaId, ct)
            ?? throw new NaoEncontradoException("importacao.falha", falhaId.ToString());
        if (f.ResolvidoEm is not null) return;

        f.ResolvidoEm = DateTime.UtcNow;
        f.AtualizadoEm = f.ResolvidoEm.Value;
        f.ResolvidoPor = UsuarioIdAtual;
        f.ResolucaoNota = Truncar(string.IsNullOrWhiteSpace(nota) ? "Descartada pelo operador." : nota, 500);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Grava/atualiza a falha de EXECUÇÃO da marcação (upsert pela pendência do mesmo nº).</summary>
    /// <summary>
    /// Registra no catálogo SIGTAP um código que o SISREG mandou e nós não tínhamos.
    ///
    /// <para><b>Por que existe:</b> o catálogo é semeado à mão e cobre uma fração do SIGTAP real.
    /// Todo código novo que o SISREG passa a emitir chegava aqui e parava: o exame entrava sem
    /// tipo, aparecia em "Mapeamento pendente", e o operador não conseguia resolver porque criar
    /// um TipoExame exige um procedimento catalogado. Catalogar na chegada desfaz o beco sem
    /// saída — a pendência passa a ser resolvível na tela que já existe.</para>
    ///
    /// <para><b>O nome vem do SISREG e NÃO é o nome oficial do SIGTAP</b> — é o nome local da
    /// unidade, com as abreviações e erros de digitação dela ("ABDOMEM"). Fica marcado na
    /// descrição, porque foi confiar em nome semeado à mão que fez 51 exames de próstata serem
    /// classificados como obstétricos. Serve para identificar e mapear, não como verdade oficial.</para>
    /// </summary>
    private async Task CatalogarSigtapSeNovoAsync(string sigtapSoDigitos, string? nomeDoSisreg, CancellationToken ct)
    {
        if (sigtapSoDigitos.Length != 10) return;

        var jaExiste = await db.ProcedimentosSigtap.AsNoTracking()
            .AnyAsync(p => p.Codigo.Replace(".", "").Replace("-", "") == sigtapSoDigitos, ct);
        if (jaExiste) return;

        var nome = string.IsNullOrWhiteSpace(nomeDoSisreg)
            ? $"PROCEDIMENTO {sigtapSoDigitos}"
            : nomeDoSisreg.Trim().ToUpperInvariant();

        db.ProcedimentosSigtap.Add(new ProcedimentoSigtap
        {
            Id = Guid.CreateVersion7(),
            Codigo = FormatarSigtap(sigtapSoDigitos),
            Nome = Truncar(nome, 300)!,
            Descricao = "Cadastrado automaticamente a partir de uma importação do SISREG. "
                        + "O nome é o que o SISREG informou (nome local da unidade), NÃO o nome "
                        + "oficial do SIGTAP — confira na tabela oficial antes de usar como referência.",
            Grupo = "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA",
            Subgrupo = "(a conferir — veio do SISREG)",
            Forma = "EXAMES",
            Ativo = true,
            CompetenciaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        logger.LogWarning(
            "SIGTAP_CODIGO_NOVO: {Codigo} (\"{Nome}\") não estava no catálogo e foi cadastrado a "
            + "partir da importação do SISREG. Confira o nome na tabela oficial e crie o tipo de "
            + "exame para tirar as solicitações de \"Mapeamento pendente\".",
            sigtapSoDigitos, nome);
    }

    /// <summary>10 dígitos → <c>NN.NN.NN.NNN-N</c>, o formato do catálogo.</summary>
    private static string FormatarSigtap(string d) =>
        $"{d[..2]}.{d[2..4]}.{d[4..6]}.{d[6..9]}-{d[9]}";

    /// <summary>
    /// Avisar o paciente por WhatsApp ao importar esta marcação? Exige que o gatilho da UNIDADE
    /// executante <b>e</b> o do PROCEDIMENTO naquela unidade estejam ligados.
    ///
    /// <para><b>É opt-in: ausência de configuração = NÃO enviar.</b> Unidade sem linha de
    /// configuração e procedimento fora do mapeamento não avisam ninguém. Mensagem ao paciente só
    /// sai depois que alguém decidiu, explicitamente, que deve sair — decisão do operador em
    /// 03/08/2026.</para>
    /// </summary>
    private async Task<bool> DeveEnviarConfirmacaoAsync(
        Guid unidadeExecutanteId, MarcacaoSisreg m, CancellationToken ct)
    {
        var daUnidade = await db.SisregVarreduraAgendas.AsNoTracking()
            .Where(a => a.UnidadeId == unidadeExecutanteId)
            .Select(a => (bool?)a.EnviarConfirmacao)
            .FirstOrDefaultAsync(ct);

        // Null = unidade nunca configurada. Não envia.
        if (daUnidade is not true) return false;

        // A varredura traz o código do SISREG. A importação por ARQUIVO não — e para essa (que
        // está em extinção) resolvemos os códigos equivalentes pelo de-para, para o mesmo exame
        // não se comportar diferente conforme o caminho pelo qual entrou.
        var codigos = !string.IsNullOrWhiteSpace(m.CodigoProcedimentoSisreg)
            ? [m.CodigoProcedimentoSisreg!.Trim()]
            : await mapeadorSigtap.ResolverCodigosPorSigtapAsync(m.CodigoSigtap ?? string.Empty, ct);

        if (codigos.Count == 0) return false;

        // Basta UM procedimento ligado com esse código na unidade. Nenhum ligado — ou nenhum
        // encontrado — não envia.
        return await db.SisregProcedimentosProfissional.AsNoTracking()
            .AnyAsync(x => codigos.Contains(x.Codigo)
                           && x.Profissional!.UnidadeId == unidadeExecutanteId
                           && x.EnviarConfirmacao, ct);
    }

    /// <param name="origem">Execução (TXT) por padrão. A varredura carimba <c>Varredura</c>, que é
    /// o que faz o reprocesso e o modal lerem o RAW como envelope JSON em vez de linha de TXT.</param>
    private async Task RegistrarFalhaExecucaoAsync(
        MarcacaoSisreg m, string? nomeArquivo, string motivo, CausaFalhaImportacao causa, CancellationToken ct,
        OrigemFalhaImportacao origem = OrigemFalhaImportacao.Execucao)
    {
        var raw = m.LinhaRaw ?? string.Empty;
        var f = await db.SisregImportacaoFalhas.FirstOrDefaultAsync(
            x => x.CodigoSolicitacao == m.CodigoSolicitacao && x.ResolvidoEm == null, ct);

        var agora = DateTime.UtcNow;
        if (f is null)
        {
            f = new SisregImportacaoFalha { Id = Guid.CreateVersion7(), CriadoEm = agora };
            db.SisregImportacaoFalhas.Add(f);
        }

        f.CodigoSolicitacao = m.CodigoSolicitacao;
        f.HashLinha = Sha256(raw);
        f.LinhaRaw = raw;
        f.Origem = origem;
        f.Causa = causa;
        f.Motivo = Truncar(motivo, 2000);
        f.NomeArquivo = Truncar(nomeArquivo, 300);
        f.CnesExecutante = SoDigitos(m.CnesUnidadeExecutante) is { Length: 7 } c ? c : null;
        f.NomeExecutante = Truncar(m.NomeUnidadeExecutante, 300);
        f.NomePaciente = Truncar(m.NomePaciente, 300);
        f.ProcedimentoTexto = Truncar(m.ProcedimentoTexto, 500);
        f.PacienteCns = Truncar(SoDigitos(m.CnsPaciente) is { Length: > 0 } cns ? cns : null, 15);
        f.DataAgendada = m.DataHoraAtendimento is { } dh ? ParaUtcBrasilia(dh) : null;
        f.ExecucaoId = _execucaoAtual;
        f.UnidadeExecutanteId = UnidadeAtivaAtual;
        f.Tentativas++;
        f.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Grava as linhas que o parser rejeitou. Dedupe pelo hash do RAW (não há nº para usar).</summary>
    private async Task RegistrarRejeitadasAsync(AgendaTxtParser.Resultado parsed, string? nomeArquivo, CancellationToken ct)
    {
        if (parsed.Rejeitadas.Count == 0) return;

        var hashes = parsed.Rejeitadas.Select(r => Sha256(r.LinhaRaw)).ToList();
        var pendentes = await db.SisregImportacaoFalhas
            .Where(f => f.CodigoSolicitacao == null && f.ResolvidoEm == null && hashes.Contains(f.HashLinha))
            .ToDictionaryAsync(f => f.HashLinha, ct);

        var agora = DateTime.UtcNow;
        var cnes = SoDigitos(parsed.Cabecalho.CnesUnidade) is { Length: 7 } c ? c : null;
        // Um mesmo arquivo pode repetir a linha ruim: agrupa por hash para não violar o índice único.
        foreach (var grupo in parsed.Rejeitadas.GroupBy(r => Sha256(r.LinhaRaw)))
        {
            var r = grupo.First();
            if (!pendentes.TryGetValue(grupo.Key, out var f))
            {
                f = new SisregImportacaoFalha
                {
                    Id = Guid.CreateVersion7(),
                    CriadoEm = agora,
                    HashLinha = grupo.Key,
                    LinhaRaw = r.LinhaRaw,
                    Origem = OrigemFalhaImportacao.Parser,
                };
                db.SisregImportacaoFalhas.Add(f);
            }
            f.Causa = CausaFalhaImportacao.LinhaInvalida;
            f.Motivo = Truncar($"Linha {r.Numero} do arquivo: {r.Motivo}", 2000);
            f.NomeArquivo = Truncar(nomeArquivo, 300);
            f.CnesExecutante = cnes;
            f.NomeExecutante = Truncar(parsed.Cabecalho.NomeUnidade, 300);
            f.ExecucaoId = _execucaoAtual;
            f.UnidadeExecutanteId = UnidadeAtivaAtual;
            f.Tentativas++;
            f.AtualizadoEm = agora;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Fecha a pendência do nº quando ele finalmente entrou (ou já estava) no sistema.</summary>
    private async Task ResolverFalhaPendenteAsync(string codigo, Guid? solicitacaoId, string nota, CancellationToken ct)
    {
        var f = await db.SisregImportacaoFalhas.FirstOrDefaultAsync(
            x => x.CodigoSolicitacao == codigo && x.ResolvidoEm == null, ct);
        if (f is null) return;

        f.ResolvidoEm = DateTime.UtcNow;
        f.AtualizadoEm = f.ResolvidoEm.Value;
        f.ResolvidoPor = UsuarioIdAtual;
        f.ResolucaoNota = Truncar(nota, 500);
        f.SolicitacaoId = solicitacaoId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cabeçalho sintético (CNES;nome;;;0) + a linha — o mínimo que o parser lê como arquivo.</summary>
    private static string ReconstruirConteudo(SisregImportacaoFalha f) =>
        f.CnesExecutante is { Length: 7 }
            ? $"{f.CnesExecutante};{f.NomeExecutante};;;0\n{f.LinhaRaw}"
            : f.LinhaRaw;

    private static string Sha256(string s) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
    private static string? Truncar(string? s, int max) =>
        s is null || s.Length <= max ? s : s[..max];

    // ===================== helpers =====================

    private static AgendaTxtParser.Resultado ParseOuFalhar(string conteudo, string? nomeArquivo)
    {
        var parsed = ParseArquivo(conteudo, nomeArquivo);
        ExigirMarcacoes(parsed);
        return parsed;
    }

    /// <summary>Só o parse (rejeita arquivo vazio). Um arquivo 100% malformado ainda volta com as
    /// <c>Rejeitadas</c> preenchidas — elas precisam ser gravadas ANTES de reclamar da ausência
    /// de marcações, senão o operador fica sem saber o que o parser recusou.</summary>
    private static AgendaTxtParser.Resultado ParseArquivo(string conteudo, string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ValidacaoException("importacao.arquivo_vazio", "Arquivo vazio ou ilegível.");
        return AgendaTxtParser.Parse(conteudo, nomeArquivo);
    }

    private static void ExigirMarcacoes(AgendaTxtParser.Resultado parsed)
    {
        if (parsed.Marcacoes.Count > 0) return;
        throw new ValidacaoException("importacao.sem_registros",
            parsed.Rejeitadas.Count > 0
                ? $"Nenhuma marcação legível: as {parsed.Rejeitadas.Count} linha(s) do arquivo foram recusadas pelo parser e estão na aba Erros. Confirme que é o export de agendamentos do SISREG (TXT ou CSV)."
                : "Não encontrei marcações no arquivo. Confirme que é o export de agendamentos do SISREG (TXT ou CSV).");
    }

    private async Task<HashSet<string>> SigtapComTipoAsync(CancellationToken ct) =>
        (await db.TiposExame.AsNoTracking()
            .Where(t => t.ExcluidoEm == null && t.Ativo && t.ProcedimentoSigtap != null)
            .Select(t => t.ProcedimentoSigtap!.Codigo)
            .ToListAsync(ct))
        .Select(SoDigitos).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);

    /// <summary>Unidade executante resolvida para o arquivo (uma só). <see cref="Erro"/> não-nulo
    /// quando não foi possível determinar (aí a importação não prossegue).</summary>
    private sealed record ExecutanteResolvido(Guid? Id, string? Nome, string? Cnes, bool Criada, string? Erro);

    /// <summary>
    /// Resolve a unidade EXECUTORA do arquivo. Prioridade:
    ///  1. <b>Tenant atual</b> (header <c>X-Unidade-Id</c>) — o contexto de unidade em que o
    ///     operador está importando. É a atribuição explícita e vale para TXT e CSV.
    ///  2. <b>CNES do cabeçalho do arquivo</b> (só o TXT traz) — fallback quando não há tenant
    ///     (ex.: admin global sem unidade ativa); resolve/cria por CNES.
    ///  3. Sem tenant e sem CNES no arquivo (CSV fora de contexto) → erro pedindo para o operador
    ///     entrar no contexto da unidade.
    /// </summary>
    private async Task<ExecutanteResolvido> ResolverExecutanteAsync(string? cnesArquivo, string? nomeExecArquivo, CancellationToken ct)
    {
        if (UnidadeAtivaAtual is { } uid)
        {
            var u = await db.Unidades.AsNoTracking()
                .Where(x => x.Id == uid && x.Ativo)
                .Select(x => new { x.Id, x.Nome, x.Cnes })
                .FirstOrDefaultAsync(ct);
            if (u is not null) return new(u.Id, u.Nome, u.Cnes, false, null);
            // Header presente mas unidade inexistente/inativa: cai no fallback do arquivo.
        }

        if (SoDigitos(cnesArquivo) is { Length: 7 } c)
        {
            var (id, criada) = await ResolverOuCriarUnidadeAsync(c, nomeExecArquivo, ct);
            if (id is not null) return new(id.Value, nomeExecArquivo, c, criada, null);
        }

        return new(null, null, null, false,
            "Selecione a unidade executante (entre no contexto da unidade) antes de importar — o arquivo não traz o CNES do executante.");
    }

    /// <summary>Resolve unidade por CNES; cria (nome UPPERCASE) se não existir. Retorna (id, criada).</summary>
    private async Task<(Guid? id, bool criada)> ResolverOuCriarUnidadeAsync(string? cnes, string? nome, CancellationToken ct)
    {
        var cnesLimpo = SoDigitos(cnes);
        if (cnesLimpo.Length != 7) return (null, false);

        var existente = await db.Unidades.Where(u => u.Cnes == cnesLimpo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (existente is not null) return (existente, false);

        var u = new Unidade
        {
            Id = Guid.CreateVersion7(),
            Nome = (string.IsNullOrWhiteSpace(nome) ? $"UNIDADE CNES {cnesLimpo}" : nome).Trim().ToUpperInvariant(),
            Cnes = cnesLimpo,
            Ativo = true,
            Externa = false,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync(ct);
        return (u.Id, true);
    }

    private static string CnesSufixo(string? cnes) =>
        SoDigitos(cnes) is { Length: 7 } c ? $" (CNES {c})" : " (sem CNES)";

    /// <summary>Telefone do TXT em slot NÃO-principal: celular se for móvel (11 díg. e 3º = '9'),
    /// senão residencial. Nunca o Principal (esse é o contato validado por OTP).</summary>
    private static (string? celular, string? residencial) MontarTelefoneDoTxt(string? telefone)
    {
        var d = SoDigitos(telefone);
        if (d.Length is < 10 or > 11) return (null, null);
        var movel = d.Length == 11 && d[2] == '9';
        return movel ? (d, null) : (null, d);
    }

    /// <summary>Monta o EnderecoDto a partir das colunas do TXT. Null se não houver nada útil.</summary>
    private static EnderecoDto? MontarEnderecoDoTxt(MarcacaoSisreg m)
    {
        var logradouro = string.Join(' ', new[] { m.TipoLogradouro, m.Logradouro }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var temAlgo = !string.IsNullOrWhiteSpace(logradouro) || !string.IsNullOrWhiteSpace(m.Cep)
            || !string.IsNullOrWhiteSpace(m.Bairro) || !string.IsNullOrWhiteSpace(m.MunicipioResidencia);
        if (!temAlgo) return null;

        return new EnderecoDto(
            Cep: SoDigitos(m.Cep) is { Length: 8 } cep ? cep : string.Empty,
            Logradouro: logradouro,
            Numero: string.IsNullOrWhiteSpace(m.Numero) ? null : m.Numero,
            Complemento: m.Complemento,
            Bairro: m.Bairro ?? string.Empty,
            Cidade: m.MunicipioResidencia ?? string.Empty,
            Uf: UfDoIbge(m.CodigoIbgeResidencia),
            PontoReferencia: null);
    }

    /// <summary>UF a partir dos 2 primeiros dígitos do código IBGE do município (33→RJ, etc.).</summary>
    private static string UfDoIbge(string? ibge)
    {
        var d = SoDigitos(ibge);
        if (d.Length < 2) return string.Empty;
        return d[..2] switch
        {
            "11" => "RO", "12" => "AC", "13" => "AM", "14" => "RR", "15" => "PA", "16" => "AP", "17" => "TO",
            "21" => "MA", "22" => "PI", "23" => "CE", "24" => "RN", "25" => "PB", "26" => "PE", "27" => "AL", "28" => "SE", "29" => "BA",
            "31" => "MG", "32" => "ES", "33" => "RJ", "35" => "SP",
            "41" => "PR", "42" => "SC", "43" => "RS",
            "50" => "MS", "51" => "MT", "52" => "GO", "53" => "DF",
            _ => string.Empty,
        };
    }

    /// <summary>Fuso de Brasília (GMT-3, sem horário de verão desde 2019 — datas de 2026 não têm DST).</summary>
    private static readonly TimeSpan OffsetBrasilia = TimeSpan.FromHours(-3);

    /// <summary>Interpreta a data/hora como wall-clock de Brasília (-03:00) e devolve o instante em UTC.</summary>
    private static DateTime ParaUtcBrasilia(DateTime wallClock) =>
        new DateTimeOffset(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), OffsetBrasilia).UtcDateTime;

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string Mascara(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : v.Length <= 4 ? "***" : v[..3] + "***" + v[^2..];
}

internal static class DataHoraExtensions
{
    public static DateOnly ToDateOnly(this DateTime dt) => DateOnly.FromDateTime(dt);
}
