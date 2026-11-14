"""Offline regression cases; never contact a real Editor or broker queue."""
import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('ops_safety', Path(__file__).parents[1]/'scripts/unity_ops.py')
ops = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ops)


class SafetyGates(unittest.TestCase):
    def args(self, *args):
        return ops.build_parser().parse_args(args)

    def test_empty_filter_blocked_before_network(self):
        with patch.object(ops.StreamableHttpClient, 'call', side_effect=AssertionError('network')) as call:
            for command in [('tests', 'lilith'), ('tool-call', 'lilith', 'run_tests')]:
                with self.subTest(command=command), self.assertRaises(ops.CommandError):
                    args = self.args(*command)
                    args.handler(args)
            call.assert_not_called()

    def test_forbidden_and_broad_filters(self):
        invalid = [[], ['RoomGenerationTests'], ['RoomGenerationSemanticsTests.TestOne'],
                   ['Lilith.RoomLayoutGeneratorTests'], ['BackPack*'], ['SafeTests|RoomGenerationTests'], ['Lilith.Tests'], [None]]
        for names in invalid:
            with self.subTest(names=names), self.assertRaises(ops.CommandError):
                ops.validate_test_selection('Lilith', {'test_names': names})
        with self.assertRaises(ops.CommandError):
            ops.validate_test_selection('lilith', {'test_names': ['SafeTests'], 'category_names': ['All']})

    def test_exact_selection_reaches_client_unchanged(self):
        arguments = {'test_names': ['RuntimeAutomationCoreTests.TestRoundTrip']}
        with patch.object(ops.StreamableHttpClient, 'call', return_value={'job_id': 'fake'}) as call:
            args=self.args('tests','lilith','--arguments',json.dumps(arguments))
            args.handler(args)
            call.assert_called_once_with('tools/call', {'name':'run_tests','arguments':dict(arguments, mode='EditMode')})

    def test_nested_batch_cannot_bypass_test_gate(self):
        args={'commands':[{'tool':'batch_execute','params':{'commands':[{'tool':'run_tests','params':{}}]}}]}
        with self.assertRaises(ops.CommandError):
            ops.validate_operation('lilith','tool-call','batch_execute',args)

    def test_other_projects_keep_their_test_policy(self):
        ops.validate_test_selection('unity4', {})
        with self.assertRaises(ops.CommandError):
            ops.validate_operation('unity4','tool-call','run_tests',{'mode':'PlayMode'})

    def test_mislabeled_write_unknown_and_nested_write_rejected(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            cases=[('refresh_unity',{}),('unknown_tool',{}),('read_console',{'action':'clear'}),
                   ('batch_execute',{'commands':[{'tool':'refresh_unity','params':{}}]})]
            for name,params in cases:
                with self.subTest(name=name),self.assertRaises(ops.CommandError):
                    args=self.args('broker','submit','lilith','--kind','tool-call','--name',name,
                                   '--requester-role','worker','--arguments',json.dumps(params),'--state-root',root)
                    ops.broker_submit(args)
            self.assertFalse(list((Path(root)/'pending').glob('*.json')))

    def submit(self, root, rid, project='lilith'):
        return ops.broker_submit(self.args('broker','submit',project,'--kind','tool-call','--name','read_console',
                                          '--requester-role','tester','--request-id',rid,'--state-root',str(root)))

    def claim(self,root,rid,project='lilith'):
        return ops.broker_claim(self.args('broker','claim',project,'--request-id',rid,'--state-root',str(root)))

    def test_claim_exact_id_and_mutual_exclusion_until_terminal(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'first');self.submit(root,'second')
            self.assertEqual('second',self.claim(root,'second')['request']['request_id'])
            with self.assertRaises(ops.CommandError): self.claim(root,'first')
            self.assertEqual(1,len(list((Path(root)/'claimed').glob('*.json'))))
            ops.broker_complete(self.args('broker','complete','second','--state-root',root))
            self.assertEqual('first',self.claim(root,'first')['request']['request_id'])
            ops.broker_fail(self.args('broker','fail','first','--error','fixture stopped','--state-root',root))
            self.submit(root,'third')
            self.assertEqual('claimed',self.claim(root,'third')['state'])

    def test_missing_exact_id_does_not_claim_another(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'other')
            self.assertEqual('empty',self.claim(root,'missing')['state'])
            self.assertTrue((Path(root)/'pending/other.json').is_file())

    def test_two_concurrent_claimers_only_one_succeeds(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'a');self.submit(root,'b')
            barrier=threading.Barrier(2);outcomes=[]
            def claim(rid):
                barrier.wait()
                try: outcomes.append(self.claim(root,rid)['state'])
                except ops.CommandError: outcomes.append('blocked')
            threads=[threading.Thread(target=claim,args=(rid,)) for rid in ['a','b']]
            for t in threads:t.start()
            for t in threads:t.join(timeout=5)
            self.assertCountEqual(['claimed','blocked'],outcomes)

    def test_different_endpoints_can_claim(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'a');self.submit(root,'b','unity4')
            self.claim(root,'a')
            self.assertEqual('claimed',self.claim(root,'b','unity4')['state'])

    def test_reusing_terminal_id_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'one');self.claim(root,'one')
            ops.broker_complete(self.args('broker','complete','one','--state-root',root))
            with self.assertRaises(ops.CommandError):self.submit(root,'one')

    def test_broker_test_gate_rechecks_pending_at_claim(self):
        with tempfile.TemporaryDirectory(prefix='unity-safety-') as root:
            self.submit(root,'tampered')
            path=Path(root)/'pending/tampered.json'
            request=json.loads(path.read_text())
            request['operation']={'kind':'tool-call','name':'run_tests','arguments':{}}
            path.write_text(json.dumps(request))
            with self.assertRaises(ops.CommandError):self.claim(root,'tampered')


if __name__=='__main__':unittest.main()
