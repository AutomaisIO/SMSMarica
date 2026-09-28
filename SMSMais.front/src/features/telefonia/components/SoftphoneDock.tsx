import { useEffect, useState } from 'react';
import { Delete, Grid3x3, Mic, MicOff, Pause, Phone, PhoneIncoming, PhoneOff, Play, X } from 'lucide-react';
import { useMeuSoftphone } from '@/features/telefonia/api/queries';
import {
  alternarEspera,
  alternarMudo,
  atender,
  desligar,
  desligarSoftphone,
  enviarDigito,
  ligarPara,
  ligarSoftphone,
} from '@/features/telefonia/lib/motorSoftphone';
import { useSoftphone, type EstadoRegistro } from '@/features/telefonia/store/softphoneStore';
import { cn } from '@/shared/lib/cn';

const TECLAS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'];

const ROTULO_REGISTRO: Record<EstadoRegistro, string> = {
  desligado: 'Desligado',
  outraAba: 'Ativo em outra aba',
  conectando: 'Conectando…',
  registrado: 'Pronto para ligar',
  falhou: 'Sem registro',
};

const COR_REGISTRO: Record<EstadoRegistro, string> = {
  desligado: 'bg-gray-400',
  outraAba: 'bg-gray-400',
  conectando: 'bg-amber-400',
  registrado: 'bg-green-500',
  falhou: 'bg-red-500',
};

/**
 * Softphone flutuante, montado no Layout ao lado do chat. Só existe para quem tem softphone
 * habilitado e ativo — o admin liga na edição do usuário. Registra sozinho ao entrar no painel.
 */
export function SoftphoneDock() {
  const meu = useMeuSoftphone();
  const ativo = !!meu.data?.habilitado && !!meu.data?.ativo;

  useEffect(() => {
    if (!ativo) return;
    ligarSoftphone();
    return () => desligarSoftphone();
  }, [ativo]);

  if (!ativo) return null;
  return <Painel />;
}

function Painel() {
  const { registro, mensagem, ramal, chamada } = useSoftphone();
  const [aberto, setAberto] = useState(false);
  const [numero, setNumero] = useState('');
  const [tecladoEmChamada, setTecladoEmChamada] = useState(false);

  // Ligação entrando abre o painel, mesmo recolhido.
  useEffect(() => {
    if (chamada?.fase === 'tocando') setAberto(true);
  }, [chamada?.fase]);

  if (!aberto) {
    return (
      <button
        type="button"
        onClick={() => setAberto(true)}
        title={`Softphone ${ramal ?? ''} — ${ROTULO_REGISTRO[registro]}`}
        className={cn(
          'fixed bottom-4 right-20 z-40 flex h-14 w-14 items-center justify-center rounded-full text-white shadow-lg',
          chamada ? 'animate-pulse bg-green-600 hover:bg-green-700' : 'bg-gray-800 hover:bg-gray-900',
        )}
      >
        <Phone className="h-6 w-6" />
        <span className={cn('absolute right-1 top-1 h-3 w-3 rounded-full ring-2 ring-white', COR_REGISTRO[registro])} />
      </button>
    );
  }

  const podeLigar = registro === 'registrado' && !chamada && numero.trim().length > 0;

  return (
    <div className="fixed bottom-4 right-20 z-40 w-72 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-2xl">
      <div className="flex items-center justify-between bg-gray-800 px-3 py-2 text-white">
        <div className="flex items-center gap-2 text-sm">
          <span className={cn('h-2.5 w-2.5 rounded-full', COR_REGISTRO[registro])} />
          <span className="font-semibold">Ramal {ramal ?? '—'}</span>
          <span className="text-xs text-gray-300">{ROTULO_REGISTRO[registro]}</span>
        </div>
        <button type="button" onClick={() => setAberto(false)} className="rounded p-1 hover:bg-gray-700" title="Recolher">
          <X className="h-4 w-4" />
        </button>
      </div>

      {mensagem && registro !== 'registrado' ? (
        <p className="border-b border-amber-100 bg-amber-50 px-3 py-2 text-xs text-amber-800">{mensagem}</p>
      ) : null}
      {registro === 'outraAba' ? (
        <p className="px-3 py-3 text-xs text-gray-600">
          O softphone está funcionando em outra aba do painel. Feche aquela aba para usar este aqui.
        </p>
      ) : null}

      {chamada ? (
        <div className="space-y-3 p-3">
          <div className="text-center">
            <p className="text-xs uppercase tracking-wide text-gray-500">
              {chamada.fase === 'tocando'
                ? 'Ligação recebida'
                : chamada.fase === 'chamando'
                  ? 'Chamando…'
                  : chamada.emEspera
                    ? 'Em espera'
                    : 'Em ligação'}
            </p>
            <p className="text-lg font-semibold text-gray-900">{chamada.nome || chamada.numero}</p>
            {chamada.nome ? <p className="text-sm text-gray-500">{chamada.numero}</p> : null}
            {chamada.inicio ? <Cronometro inicio={chamada.inicio} /> : null}
          </div>

          {chamada.fase === 'tocando' ? (
            <div className="flex justify-center gap-6">
              <BotaoRedondo cor="bg-green-600 hover:bg-green-700" titulo="Atender" onClick={atender}>
                <PhoneIncoming className="h-6 w-6" />
              </BotaoRedondo>
              <BotaoRedondo cor="bg-red-600 hover:bg-red-700" titulo="Recusar" onClick={desligar}>
                <PhoneOff className="h-6 w-6" />
              </BotaoRedondo>
            </div>
          ) : (
            <>
              {chamada.fase === 'emCurso' ? (
                <div className="flex justify-center gap-2">
                  <BotaoAcao ativo={chamada.mudo} titulo={chamada.mudo ? 'Reativar microfone' : 'Silenciar'} onClick={alternarMudo}>
                    {chamada.mudo ? <MicOff className="h-4 w-4" /> : <Mic className="h-4 w-4" />}
                  </BotaoAcao>
                  <BotaoAcao ativo={chamada.emEspera} titulo={chamada.emEspera ? 'Retomar' : 'Pôr em espera'} onClick={alternarEspera}>
                    {chamada.emEspera ? <Play className="h-4 w-4" /> : <Pause className="h-4 w-4" />}
                  </BotaoAcao>
                  <BotaoAcao ativo={tecladoEmChamada} titulo="Teclado" onClick={() => setTecladoEmChamada((v) => !v)}>
                    <Grid3x3 className="h-4 w-4" />
                  </BotaoAcao>
                </div>
              ) : null}
              {tecladoEmChamada && chamada.fase === 'emCurso' ? <Teclado aoTeclar={enviarDigito} /> : null}
              <div className="flex justify-center">
                <BotaoRedondo cor="bg-red-600 hover:bg-red-700" titulo="Desligar" onClick={desligar}>
                  <PhoneOff className="h-6 w-6" />
                </BotaoRedondo>
              </div>
            </>
          )}
        </div>
      ) : registro !== 'outraAba' ? (
        <form
          className="space-y-3 p-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (podeLigar) ligarPara(numero);
          }}
        >
          <div className="flex items-center gap-1 rounded-md border border-gray-300 px-2">
            <input
              value={numero}
              onChange={(e) => setNumero(e.target.value.replace(/[^\d*#+]/g, ''))}
              placeholder="Ramal ou número"
              inputMode="tel"
              className="w-full border-0 py-2 text-lg tracking-wider focus:outline-none focus:ring-0"
            />
            {numero ? (
              <button type="button" onClick={() => setNumero((n) => n.slice(0, -1))} className="p-1 text-gray-400 hover:text-gray-700" title="Apagar">
                <Delete className="h-4 w-4" />
              </button>
            ) : null}
          </div>
          <Teclado aoTeclar={(d) => setNumero((n) => n + d)} />
          <button
            type="submit"
            disabled={!podeLigar}
            className="flex w-full items-center justify-center gap-2 rounded-md bg-green-600 py-2 text-sm font-semibold text-white hover:bg-green-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            <Phone className="h-4 w-4" /> Ligar
          </button>
        </form>
      ) : null}
    </div>
  );
}

function Teclado({ aoTeclar }: { aoTeclar: (d: string) => void }) {
  return (
    <div className="grid grid-cols-3 gap-1.5">
      {TECLAS.map((t) => (
        <button
          key={t}
          type="button"
          onClick={() => aoTeclar(t)}
          className="rounded-md bg-gray-100 py-2 text-base font-medium text-gray-800 hover:bg-gray-200"
        >
          {t}
        </button>
      ))}
    </div>
  );
}

function BotaoRedondo({ cor, titulo, onClick, children }: { cor: string; titulo: string; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" title={titulo} onClick={onClick} className={cn('flex h-12 w-12 items-center justify-center rounded-full text-white', cor)}>
      {children}
    </button>
  );
}

function BotaoAcao({ ativo, titulo, onClick, children }: { ativo: boolean; titulo: string; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      title={titulo}
      onClick={onClick}
      className={cn(
        'flex h-9 w-9 items-center justify-center rounded-full border',
        ativo ? 'border-primary-600 bg-primary-600 text-white' : 'border-gray-300 text-gray-700 hover:bg-gray-100',
      )}
    >
      {children}
    </button>
  );
}

function Cronometro({ inicio }: { inicio: number }) {
  const [agora, setAgora] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setAgora(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);
  const s = Math.max(0, Math.floor((agora - inicio) / 1000));
  const mm = String(Math.floor(s / 60)).padStart(2, '0');
  const ss = String(s % 60).padStart(2, '0');
  return <p className="mt-1 font-mono text-sm text-gray-600">{mm}:{ss}</p>;
}
