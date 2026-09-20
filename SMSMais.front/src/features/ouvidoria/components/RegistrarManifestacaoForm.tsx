import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Info, Loader2, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { apenasDigitosCpf, cpfValido } from '@/shared/lib/cpf';
import { hojeSP } from '@/shared/lib/datas';
import { AjudaCampo } from '@/shared/ui/AjudaCampo';
import { BuscaPaciente } from '@/shared/ui/BuscaPaciente';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useRegistrarManifestacao } from '@/features/ouvidoria/api/queries';
import { AnexosOuvidoriaInput } from '@/features/ouvidoria/components/AnexosOuvidoria';
import { SeletorAssunto, SeletorUnidade } from '@/features/ouvidoria/components/Seletores';
import { Textarea } from '@/features/ouvidoria/components/Textarea';
import { identificacoesPermitidas } from '@/features/ouvidoria/lib/regras';
import {
  CANAIS,
  IDENTIFICACOES,
  ORIGENS,
  ROTULO_CANAL,
  ROTULO_IDENTIFICACAO,
  ROTULO_ORIGEM,
  ROTULO_TIPO,
  TIPOS,
} from '@/features/ouvidoria/lib/rotulos';
import type {
  ManifestacaoCriadaDto,
  OuvidoriaCanal,
  OuvidoriaIdentificacao,
  OuvidoriaOrigem,
  OuvidoriaTipo,
  RegistrarManifestacaoRequest,
} from '@/features/ouvidoria/types';

const DICA_TIPO: Record<OuvidoriaTipo, string> = {
  Solicitacao: 'Pedido de atendimento, exame, medicamento, transporte… Sempre identificada.',
  Reclamacao: 'Insatisfação com um serviço ou atendimento prestado.',
  Denuncia: 'Relato de irregularidade ou ilícito. Pode ser sigilosa ou anônima.',
  Sugestao: 'Ideia para melhorar um serviço.',
  Elogio: 'Reconhecimento a um serviço ou profissional.',
  Informacao: 'Pedido de informação sobre serviços, horários, fluxos. Sempre identificada.',
};

const anexoRef = z.object({ midiaId: z.string(), nomeArquivo: z.string() });
const opcional = z.string().trim().optional().or(z.literal(''));

const schema = z
  .object({
    tipo: z.enum(TIPOS as [OuvidoriaTipo, ...OuvidoriaTipo[]]),
    identificacao: z.enum(IDENTIFICACOES as [OuvidoriaIdentificacao, ...OuvidoriaIdentificacao[]]),
    canal: z.enum(CANAIS as [OuvidoriaCanal, ...OuvidoriaCanal[]]),
    origem: z.enum(ORIGENS as [OuvidoriaOrigem, ...OuvidoriaOrigem[]]),
    teor: z.string().trim().min(10, 'Descreva a manifestação com pelo menos 10 caracteres.'),
    resumo: z.string().trim().max(200, 'Máximo de 200 caracteres.').optional().or(z.literal('')),
    assuntoId: z.string().nullable(),
    subassuntoId: z.string().nullable(),
    unidadeId: opcional,
    dataFato: opcional,
    localFato: z.string().trim().max(200).optional().or(z.literal('')),
    manifestanteNome: z.string().trim().max(200).optional().or(z.literal('')),
    manifestanteCpf: opcional,
    manifestanteTelefone: z.string().trim().max(20).optional().or(z.literal('')),
    manifestanteEmail: z.string().trim().email('E-mail inválido.').optional().or(z.literal('')),
    manifestantePatientId: z.string().nullable(),
    referidoNome: z.string().trim().max(200).optional().or(z.literal('')),
    referidoCpf: opcional,
    referidoCns: z.string().trim().max(15).optional().or(z.literal('')),
    referidoPatientId: z.string().nullable(),
    envolvidoDescricao: z.string().trim().max(300, 'Máximo de 300 caracteres.').optional().or(z.literal('')),
    protocoloExterno: z.string().trim().max(60).optional().or(z.literal('')),
    sistemaExterno: z.string().trim().max(40).optional().or(z.literal('')),
    anexos: z.array(anexoRef),
  })
  .superRefine((v, ctx) => {
    if (!identificacoesPermitidas(v.tipo).includes(v.identificacao)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['identificacao'],
        message: `${ROTULO_TIPO[v.tipo]} não admite manifestação ${v.identificacao === 'Anonima' ? 'anônima' : 'sigilosa'}.`,
      });
    }
    const cpf = apenasDigitosCpf(v.manifestanteCpf ?? '');
    if (cpf && !cpfValido(cpf)) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['manifestanteCpf'], message: 'CPF inválido.' });
    }
    const rcpf = apenasDigitosCpf(v.referidoCpf ?? '');
    if (rcpf && !cpfValido(rcpf)) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['referidoCpf'], message: 'CPF inválido.' });
    }
    if (v.identificacao === 'Identificada') {
      const temNome = !!v.manifestanteNome?.trim();
      const temContato = !!v.manifestanteTelefone?.trim() || !!v.manifestanteEmail?.trim();
      const exigeContato = v.tipo === 'Solicitacao' || v.tipo === 'Informacao';
      // Lei 13.460 art. 10-A: CPF basta; senão, nome + um contato para poder responder.
      if (exigeContato && !cpf && !(temNome && temContato)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['manifestanteNome'],
          message: 'Manifestação identificada: informe o CPF, ou o nome com telefone/e-mail.',
        });
      } else if (!exigeContato && !cpf && !temNome && !temContato) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['manifestanteNome'],
          message: 'Manifestação identificada precisa de ao menos nome, CPF ou um contato.',
        });
      }
    }
  });

type Valores = z.infer<typeof schema>;

const PADRAO: Valores = {
  tipo: 'Reclamacao',
  identificacao: 'Identificada',
  canal: 'Presencial',
  origem: 'Cidadao',
  teor: '',
  resumo: '',
  assuntoId: null,
  subassuntoId: null,
  unidadeId: '',
  dataFato: '',
  localFato: '',
  manifestanteNome: '',
  manifestanteCpf: '',
  manifestanteTelefone: '',
  manifestanteEmail: '',
  manifestantePatientId: null,
  referidoNome: '',
  referidoCpf: '',
  referidoCns: '',
  referidoPatientId: null,
  envolvidoDescricao: '',
  protocoloExterno: '',
  sistemaExterno: '',
  anexos: [],
};

const ou = (s: string | undefined) => (s && s.trim() ? s.trim() : null);

function paraPayload(v: Valores): RegistrarManifestacaoRequest {
  const anonima = v.identificacao === 'Anonima';
  const manifestante = anonima
    ? null
    : {
        nome: ou(v.manifestanteNome),
        cpf: ou(apenasDigitosCpf(v.manifestanteCpf ?? '')),
        telefone: ou(v.manifestanteTelefone),
        email: ou(v.manifestanteEmail),
        patientId: v.manifestantePatientId,
      };
  const temManifestante = manifestante && Object.values(manifestante).some(Boolean);
  const referido = {
    patientId: v.referidoPatientId,
    nome: ou(v.referidoNome),
    cpf: ou(apenasDigitosCpf(v.referidoCpf ?? '')),
    cns: ou(v.referidoCns?.replace(/\D/g, '')),
  };
  const temReferido = Object.values(referido).some(Boolean);

  return {
    tipo: v.tipo,
    identificacao: v.identificacao,
    canal: v.canal,
    origem: v.origem,
    teor: v.teor.trim(),
    resumo: ou(v.resumo),
    assuntoId: v.assuntoId,
    subassuntoId: v.subassuntoId,
    unidadeId: ou(v.unidadeId),
    dataFato: ou(v.dataFato),
    localFato: ou(v.localFato),
    manifestante: temManifestante ? manifestante : null,
    referido: temReferido ? referido : null,
    envolvidoDescricao: ou(v.envolvidoDescricao),
    protocoloExterno: ou(v.protocoloExterno),
    sistemaExterno: ou(v.sistemaExterno),
    regulacaoSolicitacaoId: null,
    anexos: v.anexos,
  };
}

type Props = { aoCriar: (criada: ManifestacaoCriadaDto) => void };

export function RegistrarManifestacaoForm({ aoCriar }: Props) {
  const registrar = useRegistrarManifestacao();
  const [erroApi, setErroApi] = useState<string | null>(null);
  const [chaveForm, setChaveForm] = useState(0);

  const {
    register,
    control,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({ resolver: zodResolver(schema), defaultValues: PADRAO, mode: 'onBlur' });

  const tipo = watch('tipo');
  const identificacao = watch('identificacao');
  const permitidas = identificacoesPermitidas(tipo);
  const anonima = identificacao === 'Anonima';

  // Regra identificação × tipo em tempo real: mudou o tipo e a identificação atual não cabe → ajusta.
  useEffect(() => {
    if (!permitidas.includes(identificacao)) setValue('identificacao', permitidas[0], { shouldValidate: true });
  }, [tipo, identificacao, permitidas, setValue]);

  // Anônima: limpa os dados do manifestante para não viajar nada por engano.
  useEffect(() => {
    if (!anonima) return;
    setValue('manifestanteNome', '');
    setValue('manifestanteCpf', '');
    setValue('manifestanteTelefone', '');
    setValue('manifestanteEmail', '');
    setValue('manifestantePatientId', null);
  }, [anonima, setValue]);

  async function enviar(v: Valores) {
    setErroApi(null);
    try {
      const criada = await registrar.mutateAsync(paraPayload(v));
      reset(PADRAO);
      setChaveForm((k) => k + 1);
      aoCriar(criada);
    } catch (e) {
      setErroApi(extrairMensagemDeErro(e));
    }
  }

  const ocupado = isSubmitting || registrar.isPending;

  return (
    <form key={chaveForm} onSubmit={handleSubmit(enviar)} className="space-y-6" noValidate>
      {/* Classificação */}
      <Secao titulo="O que o cidadão traz">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Tipo" htmlFor="ouv-tipo" required dica={DICA_TIPO[tipo]} erro={errors.tipo?.message}>
            <Select id="ouv-tipo" {...register('tipo')}>
              {TIPOS.map((t) => (
                <option key={t} value={t}>
                  {ROTULO_TIPO[t]}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo
            label="Identificação"
            htmlFor="ouv-identificacao"
            required
            erro={errors.identificacao?.message}
            dica={
              permitidas.length === 1
                ? `${ROTULO_TIPO[tipo]} é sempre identificada.`
                : tipo === 'Denuncia'
                  ? 'Anônima vira comunicação de irregularidade: sem código de acesso e sem acompanhamento.'
                  : 'Sigilosa: só a ouvidoria vê quem é; a área responde sem saber.'
            }
          >
            <Select id="ouv-identificacao" {...register('identificacao')}>
              {IDENTIFICACOES.map((i) => (
                <option key={i} value={i} disabled={!permitidas.includes(i)}>
                  {ROTULO_IDENTIFICACAO[i]}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo label="Canal de entrada" htmlFor="ouv-canal" required erro={errors.canal?.message}>
            <Select id="ouv-canal" {...register('canal')}>
              {CANAIS.map((c) => (
                <option key={c} value={c}>
                  {ROTULO_CANAL[c]}
                </option>
              ))}
            </Select>
          </Campo>
          <Campo label="Origem" htmlFor="ouv-origem" required erro={errors.origem?.message}>
            <Select id="ouv-origem" {...register('origem')}>
              {ORIGENS.map((o) => (
                <option key={o} value={o}>
                  {ROTULO_ORIGEM[o]}
                </option>
              ))}
            </Select>
          </Campo>
        </div>

        <Campo
          label="Relato (teor)"
          htmlFor="ouv-teor"
          required
          erro={errors.teor?.message}
          dica="Escreva como o cidadão contou. Não há campo 'motivo' — o texto é o registro."
        >
          <Textarea id="ouv-teor" rows={6} maxLength={10000} {...register('teor')} />
        </Campo>

        <Campo label="Resumo (opcional)" htmlFor="ouv-resumo" erro={errors.resumo?.message} dica="Uma linha para a fila. Até 200 caracteres.">
          <Input id="ouv-resumo" maxLength={200} {...register('resumo')} />
        </Campo>

        <Controller
          control={control}
          name="assuntoId"
          render={({ field }) => (
            <SeletorAssunto
              idBase="ouv-assunto"
              assuntoId={field.value}
              subassuntoId={watch('subassuntoId')}
              aoMudar={(a, s) => {
                field.onChange(a);
                setValue('subassuntoId', s);
              }}
              disabled={ocupado}
            />
          )}
        />

        <div className="grid gap-3 sm:grid-cols-3">
          <Campo label="Unidade relacionada" htmlFor="ouv-unidade" erro={errors.unidadeId?.message} className="sm:col-span-1">
            <Controller
              control={control}
              name="unidadeId"
              render={({ field }) => <SeletorUnidade id="ouv-unidade" value={field.value ?? ''} onChange={field.onChange} disabled={ocupado} rotuloVazio="Não se aplica / não sei" />}
            />
          </Campo>
          <Campo label="Data do fato" htmlFor="ouv-data-fato" erro={errors.dataFato?.message}>
            <Input id="ouv-data-fato" type="date" max={hojeSP()} {...register('dataFato')} />
          </Campo>
          <Campo label="Local do fato" htmlFor="ouv-local-fato" erro={errors.localFato?.message}>
            <Input id="ouv-local-fato" maxLength={200} placeholder="Setor, sala, recepção…" {...register('localFato')} />
          </Campo>
        </div>
      </Secao>

      {/* Manifestante */}
      <Secao
        titulo="Quem manifesta"
        descricao={
          anonima
            ? 'Manifestação anônima: nenhum dado do manifestante é registrado.'
            : identificacao === 'Sigilosa'
              ? 'Os dados ficam restritos à ouvidoria. A área que responde não os vê.'
              : undefined
        }
      >
        {!anonima ? (
          <>
            <div className="mb-3">
              <span className="label flex items-center gap-1">
                Buscar cidadão já cadastrado (opcional)
                <AjudaCampo titulo="Buscar cidadão">
                  <p>Preenche nome, CPF e telefone a partir do cadastro. Você pode ajustar depois.</p>
                </AjudaCampo>
              </span>
              <BuscaPaciente
                placeholder="Nome ou CPF do manifestante"
                aoSelecionar={(p) => {
                  setValue('manifestanteNome', p.nomeCompleto, { shouldValidate: true });
                  setValue('manifestanteCpf', p.cpf ?? '', { shouldValidate: true });
                  setValue('manifestanteTelefone', p.telefonePrincipal ?? '', { shouldValidate: true });
                  setValue('manifestantePatientId', p.id);
                }}
              />
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Campo label="Nome" htmlFor="ouv-m-nome" erro={errors.manifestanteNome?.message}>
                <Input id="ouv-m-nome" maxLength={200} {...register('manifestanteNome')} />
              </Campo>
              <Campo label="CPF" htmlFor="ouv-m-cpf" erro={errors.manifestanteCpf?.message}>
                <Input id="ouv-m-cpf" inputMode="numeric" maxLength={14} placeholder="000.000.000-00" {...register('manifestanteCpf')} />
              </Campo>
              <Campo label="Telefone" htmlFor="ouv-m-telefone" erro={errors.manifestanteTelefone?.message} dica="Com DDD. É por aqui que o cidadão recebe o protocolo e as etapas (WhatsApp).">
                <Input id="ouv-m-telefone" inputMode="tel" maxLength={20} placeholder="(21) 9xxxx-xxxx" {...register('manifestanteTelefone')} />
              </Campo>
              <Campo label="E-mail" htmlFor="ouv-m-email" erro={errors.manifestanteEmail?.message}>
                <Input id="ouv-m-email" type="email" maxLength={200} {...register('manifestanteEmail')} />
              </Campo>
            </div>
          </>
        ) : (
          <p className="flex items-center gap-2 text-sm text-slate-600">
            <Info className="h-4 w-4 text-slate-400" aria-hidden="true" />
            Sem código de acesso: o cidadão não poderá acompanhar nem complementar.
          </p>
        )}
      </Secao>

      {/* Referido e envolvido */}
      <Secao titulo="Em favor de quem / sobre quem" descricao="Só quando a manifestação é sobre outra pessoa (paciente) ou aponta um agente.">
        <div className="mb-3">
          <span className="label">Paciente referido — buscar no cadastro (opcional)</span>
          <BuscaPaciente
            placeholder="Nome ou CPF do paciente em favor de quem se manifesta"
            aoSelecionar={(p) => {
              setValue('referidoNome', p.nomeCompleto);
              setValue('referidoCpf', p.cpf ?? '');
              setValue('referidoPatientId', p.id);
            }}
          />
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <Campo label="Nome do paciente" htmlFor="ouv-r-nome" erro={errors.referidoNome?.message}>
            <Input id="ouv-r-nome" maxLength={200} {...register('referidoNome')} />
          </Campo>
          <Campo label="CPF" htmlFor="ouv-r-cpf" erro={errors.referidoCpf?.message}>
            <Input id="ouv-r-cpf" inputMode="numeric" maxLength={14} {...register('referidoCpf')} />
          </Campo>
          <Campo label="CNS" htmlFor="ouv-r-cns" erro={errors.referidoCns?.message}>
            <Input id="ouv-r-cns" inputMode="numeric" maxLength={15} {...register('referidoCns')} />
          </Campo>
        </div>
        <Campo
          label="Agente/serviço envolvido"
          htmlFor="ouv-envolvido"
          erro={errors.envolvidoDescricao?.message}
          dica="Descreva como o cidadão identificou (nome, função, setor). Em denúncia, este dado não vai à área na versão pseudonimizada."
        >
          <Input id="ouv-envolvido" maxLength={300} {...register('envolvidoDescricao')} />
        </Campo>
      </Secao>

      {/* Externo e anexos */}
      <Secao titulo="Complementos">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Sistema externo" htmlFor="ouv-sistema-externo" erro={errors.sistemaExterno?.message} dica="Ex.: OuvidorSUS, Fala.BR, ouvidoria-geral.">
            <Input id="ouv-sistema-externo" maxLength={40} {...register('sistemaExterno')} />
          </Campo>
          <Campo label="Protocolo externo" htmlFor="ouv-protocolo-externo" erro={errors.protocoloExterno?.message}>
            <Input id="ouv-protocolo-externo" maxLength={60} {...register('protocoloExterno')} />
          </Campo>
        </div>
        <Campo label="Anexos" htmlFor="ouv-anexos">
          <Controller
            control={control}
            name="anexos"
            render={({ field }) => <AnexosOuvidoriaInput id="ouv-anexos" anexos={field.value} aoMudar={field.onChange} disabled={ocupado} />}
          />
        </Campo>
      </Secao>

      {erroApi ? (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {erroApi}
        </p>
      ) : null}

      <div className="flex justify-end gap-2">
        <Button type="submit" disabled={ocupado}>
          {ocupado ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Send className="h-4 w-4" aria-hidden="true" />}
          {ocupado ? 'Registrando…' : 'Registrar manifestação'}
        </Button>
      </div>
    </form>
  );
}

function Secao({ titulo, descricao, children }: { titulo: string; descricao?: string; children: React.ReactNode }) {
  return (
    <fieldset className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <legend className="px-1 text-sm font-semibold text-slate-700">{titulo}</legend>
      {descricao ? <p className="-mt-1 text-xs text-slate-500">{descricao}</p> : null}
      {children}
    </fieldset>
  );
}
