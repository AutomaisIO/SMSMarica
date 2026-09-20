import { http } from '@/shared/api/httpClient';
import type {
  AnexoRef,
  ArquivarRequest,
  AssuntoDto,
  EncaminharExternoRequest,
  EncaminharRequest,
  FiltroManifestacoes,
  ManifestacaoCriadaDto,
  ManifestacaoDetalheDto,
  ManifestacaoListaDto,
  ManifestanteDto,
  MarcadorDto,
  OuvidoriaConfiguracaoDto,
  OuvidoriaPainelDto,
  OuvidoriaResumoDto,
  PaginaDto,
  PontoRespostaDto,
  RegistrarManifestacaoRequest,
  ResponderCidadaoRequest,
  SalvarAssuntoRequest,
  SalvarMarcadorRequest,
  SalvarPontoRespostaRequest,
  TextoComAnexosRequest,
  TextoRequest,
  TriarRequest,
} from '@/features/ouvidoria/types';

/** Uma função por endpoint do `OuvidoriaController` (`/ouvidoria/...`, plano §3.1). */

const BASE = '/ouvidoria';

// ---- Manifestações ----

export async function listarManifestacoes(filtro: FiltroManifestacoes): Promise<PaginaDto<ManifestacaoListaDto>> {
  const { data } = await http.get<PaginaDto<ManifestacaoListaDto>>(`${BASE}/manifestacoes`, {
    params: {
      status: filtro.status?.length ? filtro.status : undefined,
      tipo: filtro.tipo || undefined,
      unidadeId: filtro.unidadeId || undefined,
      pontoRespostaId: filtro.pontoRespostaId || undefined,
      prioridade: filtro.prioridade || undefined,
      atrasadas: filtro.atrasadas || undefined,
      aguardandoValidacao: filtro.aguardandoValidacao || undefined,
      busca: filtro.busca?.trim() || undefined,
      de: filtro.de || undefined,
      ate: filtro.ate || undefined,
      pagina: filtro.pagina,
      tamanho: filtro.tamanho,
    },
    // `indexes: null` manda a lista como `status=A&status=B` — o formato que o ASP.NET liga.
    paramsSerializer: { indexes: null },
  });
  return data;
}

export async function obterResumo(): Promise<OuvidoriaResumoDto> {
  const { data } = await http.get<OuvidoriaResumoDto>(`${BASE}/manifestacoes/resumo`);
  return data;
}

export async function obterManifestacao(id: string): Promise<ManifestacaoDetalheDto> {
  const { data } = await http.get<ManifestacaoDetalheDto>(`${BASE}/manifestacoes/${id}`);
  return data;
}

export async function registrarManifestacao(payload: RegistrarManifestacaoRequest): Promise<ManifestacaoCriadaDto> {
  const { data } = await http.post<ManifestacaoCriadaDto>(`${BASE}/manifestacoes`, payload);
  return data;
}

export async function triar(id: string, payload: TriarRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/triagem`, payload);
}

export async function encaminhar(id: string, payload: EncaminharRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/encaminhar`, payload);
}

export async function pedirComplementacao(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/pedir-complementacao`, payload);
}

export async function complementar(id: string, payload: TextoComAnexosRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/complementar`, payload);
}

export async function responderArea(id: string, payload: TextoComAnexosRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/responder-area`, payload);
}

export async function devolverArea(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/devolver-area`, payload);
}

export async function responderCidadao(id: string, payload: ResponderCidadaoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/responder-cidadao`, payload);
}

export async function prorrogar(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/prorrogar`, payload);
}

export async function cobrar(id: string, payload: TextoRequest | null): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/cobrar`, payload ?? undefined);
}

export async function escalonar(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/escalonar`, payload);
}

export async function registrarRecurso(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/recurso`, payload);
}

export async function concluir(id: string): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/concluir`);
}

export async function arquivar(id: string, payload: ArquivarRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/arquivar`, payload);
}

export async function encaminharExterno(id: string, payload: EncaminharExternoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/encaminhar-externo`, payload);
}

export async function habilitarDenuncia(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/habilitar`, payload);
}

export async function atualizarTeorPseudonimizado(id: string, payload: TextoRequest): Promise<void> {
  await http.put(`${BASE}/manifestacoes/${id}/teor-pseudonimizado`, payload);
}

export async function anotar(id: string, payload: TextoRequest): Promise<void> {
  await http.post(`${BASE}/manifestacoes/${id}/anotar`, payload);
}

/** Revela a identidade de uma manifestação restrita. Exige justificativa; o acesso fica registrado. */
export async function revelarIdentidade(id: string, justificativa: string): Promise<ManifestanteDto> {
  const { data } = await http.post<ManifestanteDto>(`${BASE}/manifestacoes/${id}/identidade`, {
    texto: justificativa,
  } satisfies TextoRequest);
  return data;
}

/** Envia um arquivo (≤ 6 MB) e devolve a referência para anexar à manifestação/evento. */
export async function enviarAnexo(arquivo: File): Promise<AnexoRef> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  const { data } = await http.post<{ id: string; nomeArquivo: string }>(`${BASE}/anexos`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return { midiaId: data.id, nomeArquivo: data.nomeArquivo };
}

// ---- Pontos de resposta ----

export async function listarPontosResposta(): Promise<PontoRespostaDto[]> {
  const { data } = await http.get<PontoRespostaDto[]>(`${BASE}/pontos-resposta`);
  return data;
}

export async function criarPontoResposta(payload: SalvarPontoRespostaRequest): Promise<string> {
  const { data } = await http.post<string | { id: string }>(`${BASE}/pontos-resposta`, payload);
  return typeof data === 'string' ? data : data.id;
}

export async function atualizarPontoResposta(id: string, payload: SalvarPontoRespostaRequest): Promise<void> {
  await http.put(`${BASE}/pontos-resposta/${id}`, payload);
}

// ---- Assuntos ----

export async function listarAssuntos(): Promise<AssuntoDto[]> {
  const { data } = await http.get<AssuntoDto[]>(`${BASE}/assuntos`);
  return data;
}

export async function criarAssunto(payload: SalvarAssuntoRequest): Promise<string> {
  const { data } = await http.post<string | { id: string }>(`${BASE}/assuntos`, payload);
  return typeof data === 'string' ? data : data.id;
}

export async function atualizarAssunto(id: string, payload: SalvarAssuntoRequest): Promise<void> {
  await http.put(`${BASE}/assuntos/${id}`, payload);
}

// ---- Marcadores ----

export async function listarMarcadores(): Promise<MarcadorDto[]> {
  const { data } = await http.get<MarcadorDto[]>(`${BASE}/marcadores`);
  return data;
}

export async function criarMarcador(payload: SalvarMarcadorRequest): Promise<string> {
  const { data } = await http.post<string | { id: string }>(`${BASE}/marcadores`, payload);
  return typeof data === 'string' ? data : data.id;
}

export async function atualizarMarcador(id: string, payload: SalvarMarcadorRequest): Promise<void> {
  await http.put(`${BASE}/marcadores/${id}`, payload);
}

// ---- Configuração e painel ----

export async function obterConfiguracao(): Promise<OuvidoriaConfiguracaoDto> {
  const { data } = await http.get<OuvidoriaConfiguracaoDto>(`${BASE}/configuracao`);
  return data;
}

export async function atualizarConfiguracao(payload: OuvidoriaConfiguracaoDto): Promise<void> {
  await http.put(`${BASE}/configuracao`, payload);
}

export async function obterPainel(de: string, ate: string, unidadeId?: string): Promise<OuvidoriaPainelDto> {
  const { data } = await http.get<OuvidoriaPainelDto>(`${BASE}/painel`, {
    params: { de, ate, unidadeId: unidadeId || undefined },
  });
  return data;
}
