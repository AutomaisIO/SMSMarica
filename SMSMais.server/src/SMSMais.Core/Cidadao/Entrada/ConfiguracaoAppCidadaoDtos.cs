namespace SMSMais.Core.Cidadao.Entrada;

/// <summary>O que um canal (PWA ou nativo) faz na entrada.</summary>
public sealed record CanalEntradaDto(bool ConsultarEsus);

/// <summary>
/// Sistema → App do cidadão. <see cref="EsusConfigurado"/> diz se a conta da plataforma no e-SUS está
/// em Fontes de dados — sem ela, ligar a consulta não tem efeito. <see cref="Ajustes"/> é o histórico
/// (trilha de auditoria), mais recente primeiro.
/// </summary>
public sealed record ConfiguracaoAppCidadaoDto(
    CanalEntradaDto Pwa,
    CanalEntradaDto Nativo,
    bool EsusConfigurado,
    DateTime? AtualizadoEm,
    string? AtualizadoPor,
    IReadOnlyList<AjusteConfiguracaoAppCidadaoDto> Ajustes);

/// <summary>Um ajuste: quando, quem e o que mudou, já escrito para a tela.</summary>
public sealed record AjusteConfiguracaoAppCidadaoDto(DateTime Em, string? Quem, IReadOnlyList<string> Mudancas);

public sealed record AtualizarConfiguracaoAppCidadaoRequest(CanalEntradaDto? Pwa, CanalEntradaDto? Nativo);
