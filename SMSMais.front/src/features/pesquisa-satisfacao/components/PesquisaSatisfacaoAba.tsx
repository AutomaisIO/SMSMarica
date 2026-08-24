import { useEffect, useState } from 'react';
import { ExternalLink, Info, MousePointerClick, Send, Eye, CheckCheck } from 'lucide-react';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import {
  obterPesquisaConfig,
  obterPesquisaPainel,
  salvarPesquisaConfig,
  type PesquisaConfig,
  type PesquisaPainel,
} from '@/features/pesquisa-satisfacao/api/pesquisaSatisfacaoApi';

/**
 * CNES das unidades que hoje têm atendimento no hub — as únicas em que o disparo tem evento
 * para acontecer. Conde, UPA Inoã e Santa Rita.
 *
 * <p>É lista manual, e não checagem automática, porque o hub FHIR é serviço autônomo (ADR-0010)
 * e o servidor não faz agregado nele. <b>Se outra unidade passar a ter atendimento no hub, esta
 * lista precisa mudar junto</b> — senão a tela vai desencorajar uma configuração que funcionaria.</p>
 */
const CNES_COM_ATENDIMENTO = new Set(['2266733', '7164440', '2266792']);

export function PesquisaSatisfacaoAba({ unidadeId }: { unidadeId: string }) {
  const podeEditar = usePermissao('PesquisaSatisfacao', 'Edicao');
  const [config, setConfig] = useState<PesquisaConfig | null>(null);
  const [painel, setPainel] = useState<PesquisaPainel | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const [ativo, setAtivo] = useState(false);
  const [linkResponder, setLinkResponder] = useState('');
  const [linkPainel, setLinkPainel] = useState('');
  const [horas, setHoras] = useState(24);

  useEffect(() => {
    let vivo = true;
    obterPesquisaConfig(unidadeId)
      .then((c) => {
        if (!vivo) return;
        setConfig(c);
        setAtivo(c.envioWhatsAppAtivo);
        setLinkResponder(c.linkResponder ?? '');
        setLinkPainel(c.linkPainel ?? '');
        setHoras(c.horasAposAtendimento);
      })
      .catch((e) => vivo && setErro(extrairMensagemDeErro(e)));
    obterPesquisaPainel(unidadeId).then((p) => vivo && setPainel(p)).catch(() => {});
    return () => {
      vivo = false;
    };
  }, [unidadeId]);

  async function salvar() {
    setSalvando(true);
    try {
      const c = await salvarPesquisaConfig(unidadeId, {
        envioWhatsAppAtivo: ativo,
        linkResponder: linkResponder.trim() || null,
        linkPainel: linkPainel.trim() || null,
        horasAposAtendimento: horas,
      });
      setConfig(c);
      notificar('Configuração da pesquisa salva.');
    } catch (e) {
      notificar(extrairMensagemDeErro(e), 'erro');
    } finally {
      setSalvando(false);
    }
  }

  if (erro) return <p className="text-sm text-red-600">{erro}</p>;
  if (!config) return <p className="text-sm text-gray-400">Carregando…</p>;

  const temAtendimento = !!config.cnes && CNES_COM_ATENDIMENTO.has(config.cnes);

  return (
    <div className="space-y-6">
      <p className="max-w-3xl text-sm text-gray-600">
        A pesquisa é respondida <strong>fora do nosso sistema</strong>, num link da AvanteSocial
        por unidade. Nós só provocamos: mandamos o WhatsApp, contamos o clique e encaminhamos. A
        resposta nunca passa por aqui — é isso que a torna anônima.
      </p>

      {!temAtendimento && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
          <Info className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Esta unidade <strong>não tem atendimento no hub clínico</strong>, então o envio
            automático não teria evento para disparar. Hoje só Conde, UPA Inoã e Santa Rita têm.
            O link abaixo continua valendo para cartaz e QR Code.
          </span>
        </div>
      )}

      <div className="grid max-w-3xl gap-4">
        <Campo label="Link da pesquisa (AvanteSocial)" htmlFor="pesq-link" required={ativo}>
          <Input
            id="pesq-link"
            value={linkResponder}
            onChange={(e) => setLinkResponder(e.target.value)}
            placeholder="https://gestao.avantesocial.com.br/pesquisa/..."
            disabled={!podeEditar}
          />
        </Campo>

        <Campo label="Link do painel de resultados (AvanteSocial)" htmlFor="pesq-painel">
          <div className="flex items-center gap-2">
            <Input
              id="pesq-painel"
              value={linkPainel}
              onChange={(e) => setLinkPainel(e.target.value)}
              placeholder="https://gestao.avantesocial.com.br/pesquisa/painel/..."
              disabled={!podeEditar}
            />
            {config.linkPainel && (
              <a
                href={config.linkPainel}
                target="_blank"
                rel="noreferrer"
                className="inline-flex shrink-0 items-center gap-1.5 rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
              >
                <ExternalLink className="h-4 w-4" /> Abrir
              </a>
            )}
          </div>
        </Campo>

        <Campo
          label="Enviar quantas horas após o fim do atendimento"
          htmlFor="pesq-horas"
          dica="Cedo demais o paciente ainda está sintomático; tarde demais já esqueceu o detalhe."
        >
          <Input
            id="pesq-horas"
            type="number"
            min={1}
            max={168}
            value={horas}
            onChange={(e) => setHoras(Number(e.target.value))}
            disabled={!podeEditar}
            className="max-w-[140px]"
          />
        </Campo>

        <label className="flex items-start gap-2 text-sm text-gray-800">
          <input
            type="checkbox"
            checked={ativo}
            onChange={(e) => setAtivo(e.target.checked)}
            disabled={!podeEditar || !temAtendimento}
            className="mt-0.5 h-4 w-4"
          />
          <span>
            Enviar a pesquisa por WhatsApp automaticamente
            {!temAtendimento && (
              <span className="text-gray-500"> — indisponível para esta unidade</span>
            )}
          </span>
        </label>

        {podeEditar && (
          <div>
            <Button onClick={salvar} disabled={salvando}>
              {salvando ? 'Salvando…' : 'Salvar'}
            </Button>
          </div>
        )}
      </div>

      {painel && <Painel painel={painel} />}
    </div>
  );
}

function Painel({ painel }: { painel: PesquisaPainel }) {
  return (
    <div className="border-t border-gray-200 pt-5">
      <h3 className="text-sm font-semibold text-gray-900">Últimos 30 dias</h3>

      <div className="mt-3 grid gap-3 sm:grid-cols-4">
        <Cartao icone={Send} rotulo="Enviadas" valor={painel.enviadas} />
        <Cartao icone={CheckCheck} rotulo="Entregues" valor={painel.entregues} />
        <Cartao icone={Eye} rotulo="Vistas" valor={painel.vistas} />
        <Cartao
          icone={MousePointerClick}
          rotulo="Clicadas"
          valor={painel.clicadas}
          nota={
            painel.horasMedianasAteClique !== null
              ? `mediana ${painel.horasMedianasAteClique}h até o clique`
              : undefined
          }
        />
      </div>

      {/* A distinção precisa estar na tela, não só no código: um gestor lendo "clicadas" como
          "respondidas" tomaria decisão sobre uma taxa que não medimos. */}
      <p className="mt-2 text-xs text-gray-500">
        <strong>Clicadas não é respondidas.</strong> Quem clicou abriu a pesquisa; se respondeu,
        só a AvanteSocial sabe — a resposta não passa por aqui.
      </p>

      {painel.perfil.length > 0 && (
        <div className="mt-5">
          <h4 className="text-sm font-semibold text-gray-900">Perfil de quem clicou</h4>
          <table className="mt-2 w-full max-w-md text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs uppercase text-gray-500">
                <th className="pb-1 pr-4 font-medium">Sexo</th>
                <th className="pb-1 pr-4 font-medium">Faixa etária</th>
                <th className="pb-1 text-right font-medium">Cliques</th>
              </tr>
            </thead>
            <tbody>
              {painel.perfil.map((p) => (
                <tr key={`${p.sexo}-${p.faixaEtaria}`} className="border-b border-gray-100">
                  <td className="py-1.5 pr-4">{p.sexo}</td>
                  <td className="py-1.5 pr-4">{p.faixaEtaria}</td>
                  <td className="py-1.5 text-right tabular-nums">{p.cliques}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function Cartao({
  icone: Icone,
  rotulo,
  valor,
  nota,
}: {
  icone: typeof Send;
  rotulo: string;
  valor: number;
  nota?: string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 p-3">
      <div className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-gray-500">
        <Icone className="h-3.5 w-3.5" />
        {rotulo}
      </div>
      <p className="mt-1 text-2xl font-semibold tabular-nums text-gray-900">{valor}</p>
      {nota && <p className="text-xs text-gray-500">{nota}</p>}
    </div>
  );
}
