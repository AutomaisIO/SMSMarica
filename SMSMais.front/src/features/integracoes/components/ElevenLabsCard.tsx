import { useEffect, useState } from 'react';
import { ChevronDown, Loader2, Mic } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useElevenLabs, useElevenLabsVozes, useSalvarElevenLabs } from '@/features/integracoes/api';
import { BuscadorVozesPtBr } from '@/features/integracoes/components/BuscadorVozesPtBr';

export function ElevenLabsCard() {
  const podeEditar = usePermissao('IntegracoesConfig', 'Edicao');
  const config = useElevenLabs();
  const vozes = useElevenLabsVozes();
  const salvar = useSalvarElevenLabs();

  const [aberto, setAberto] = useState(false);
  const [baseUrl, setBaseUrl] = useState('https://api.elevenlabs.io/');
  const [modelo, setModelo] = useState('scribe_v2');
  const [modeloTts, setModeloTts] = useState('eleven_multilingual_v2');
  const [vozId, setVozId] = useState('');
  const [velocidade, setVelocidade] = useState(1.15);
  const [apiKey, setApiKey] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (config.data) {
      setBaseUrl(config.data.baseUrl);
      setModelo(config.data.modelo);
      setModeloTts(config.data.modeloTts);
      setVozId(config.data.vozId ?? '');
      setVelocidade(config.data.velocidadeTts);
      setAtivo(config.data.ativo);
    }
  }, [config.data]);

  const configurada = config.data?.chaveConfigurada ?? false;
  const listaVozes = vozes.data ?? [];

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setSalvo(false);
    salvar.mutate(
      {
        baseUrl: baseUrl.trim(),
        modelo: modelo.trim() || undefined,
        modeloTts: modeloTts.trim() || undefined,
        vozId: vozId.trim(),
        velocidadeTts: velocidade,
        apiKey: apiKey || undefined,
        ativo,
      },
      {
        onSuccess: () => {
          setSalvo(true);
          setApiKey('');
        },
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      },
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        type="button"
        onClick={() => setAberto((a) => !a)}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span className="flex items-center gap-2">
          <Mic className="h-5 w-5 text-primary-600" />
          <span className="font-medium text-gray-900">ElevenLabs (áudio: transcrição e voz)</span>
        </span>
        <span className="flex items-center gap-2">
          <span
            className={
              configurada && (config.data?.ativo ?? false)
                ? 'rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-medium text-green-700'
                : 'rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-500'
            }
          >
            {configurada ? (config.data?.ativo ? 'Configurado' : 'Inativo') : 'Não configurado'}
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${aberto ? 'rotate-180' : ''}`} />
        </span>
      </button>

      {aberto ? (
        <form onSubmit={aoSalvar} className="mt-4 space-y-4 border-t border-gray-100 pt-4">
          <Campo label="URL base" htmlFor="el-url">
            <Input id="el-url" value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} disabled={!podeEditar} />
          </Campo>

          <Campo label="Modelo de transcrição (STT)" htmlFor="el-modelo" dica="Padrão: scribe_v2.">
            <Input id="el-modelo" value={modelo} onChange={(e) => setModelo(e.target.value)} disabled={!podeEditar} />
          </Campo>

          <Campo label="Modelo de voz (TTS)" htmlFor="el-modelo-tts" dica="Padrão: eleven_multilingual_v2.">
            <Input id="el-modelo-tts" value={modeloTts} onChange={(e) => setModeloTts(e.target.value)} disabled={!podeEditar} />
          </Campo>

          <Campo
            label="Voz"
            htmlFor="el-voz"
            dica={
              listaVozes.length > 0
                ? 'A voz usada nas respostas em áudio. Em branco = primeira da conta.'
                : 'Cole um voice_id. (A lista de vozes só aparece com a chave válida salva.)'
            }
          >
            {listaVozes.length > 0 ? (
              <Select id="el-voz" value={vozId} onChange={(e) => setVozId(e.target.value)} disabled={!podeEditar}>
                <option value="">(primeira da conta)</option>
                {vozId && !listaVozes.some((v) => v.vozId === vozId) ? (
                  <option value={vozId}>{vozId} (atual)</option>
                ) : null}
                {listaVozes.map((v) => (
                  <option key={v.vozId} value={v.vozId}>
                    {v.nome}
                    {v.idioma ? ` — ${v.idioma}` : ''}
                  </option>
                ))}
              </Select>
            ) : (
              <Input
                id="el-voz"
                value={vozId}
                onChange={(e) => setVozId(e.target.value)}
                placeholder="(primeira da conta)"
                disabled={!podeEditar}
              />
            )}
          </Campo>

          <Campo
            label={`Velocidade da voz: ${velocidade.toFixed(2)}×`}
            htmlFor="el-vel"
            dica="0,70 (mais devagar) a 1,20 (mais rápido). Padrão 1,15."
          >
            <input
              id="el-vel"
              type="range"
              min={0.7}
              max={1.2}
              step={0.05}
              value={velocidade}
              onChange={(e) => setVelocidade(Number(e.target.value))}
              disabled={!podeEditar}
              className="w-full"
            />
          </Campo>

          <BuscadorVozesPtBr podeEditar={podeEditar} />

          <Campo
            label="Chave da API (API Key)"
            htmlFor="el-key"
            dica={configurada ? 'Já configurada — preencha apenas para substituir.' : 'Ainda não configurada.'}
          >
            <Input
              id="el-key"
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={configurada ? '••••••••••••' : 'Cole a chave da ElevenLabs aqui'}
              autoComplete="new-password"
              disabled={!podeEditar}
            />
          </Campo>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} disabled={!podeEditar} />
            Integração ativa
          </label>

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          {podeEditar ? (
            <div className="flex flex-wrap items-center justify-end gap-3">
              {salvo ? <span className="text-sm text-green-600">Salvo.</span> : null}
              <Button type="submit" disabled={salvar.isPending}>
                {salvar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
            </div>
          ) : (
            <p className="text-xs text-gray-500">Você não tem permissão para editar.</p>
          )}
        </form>
      ) : null}
    </div>
  );
}
