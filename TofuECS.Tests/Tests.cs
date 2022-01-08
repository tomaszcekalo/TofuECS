﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tofunaut.TofuECS;
using Tofunaut.TofuECS.Utilities;

namespace TofuECS.Tests
{
    [TestFixture]
    public unsafe class Tests
    {
        /*
        [Test]
        public void RollbackTests()
        {
            var s = new Simulation(new RollbackTestSimulationConfig(), new DummyECSDatabase(),
                new TestLogService(), new ISystem[]
                {
                    new SomeValueSystem(),
                });
            
            s.RegisterComponent<SomeValueComponent>();
            s.Initialize();

            var entityA = s.CurrentFrame.CreateEntity();
            s.CurrentFrame.AddComponent<SomeValueComponent>(entityA);

            var randomAt30 = 0;
            var randomAt15 = 0;
            for (var i = 0; i < 30; i++)
            {
                s.Tick();

                switch (s.CurrentFrame.Number)
                {
                    case 30:
                        randomAt30 = s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).RandomValue;
                        break;
                    case 15:
                        randomAt15 = s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).RandomValue;
                        break;
                }Ωz
            }
            
            Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).IncrementingValue == 30);
            s.RollbackTo(15);
            Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).IncrementingValue == 15);
            s.RollbackTo(2);
            Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).IncrementingValue == 2);
            
            // The simulation can "rollback" into the future if the frames have already been processed, although I'm not
            // sure why this would be desirable...
            s.RollbackTo(29);
            Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).IncrementingValue == 29);
            // ...although frame 30 has not been processed, so we cannot "rollback" to it or any frames beyond it.
            Assert.Catch<InvalidRollbackException>(() =>
            {
                s.RollbackTo(30);
            });
            
            // frame 1 is too far back to rollback to, now that we've processed 29 frames
            // ISimulationConfig.FramesInMemory - 2 is the max number of frames we can rollback to from the highest processed frame
            Assert.Catch<InvalidRollbackException>(() =>
            {
                s.RollbackTo(1);
            });
            // frame -1 is obviously invalid
            Assert.Catch<InvalidRollbackException>(() =>
            {
                s.RollbackTo(-1);
            });
            
            // make sure RNG is the same when the sim is played back
            for (var i = 0; i < 30; i++)
            {
                s.Tick();

                switch (s.CurrentFrame.Number)
                {
                    case 30:
                        Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).RandomValue == randomAt30);
                        break;
                    case 15:
                        Assert.IsTrue(s.CurrentFrame.GetComponent<SomeValueComponent>(entityA).RandomValue == randomAt15);
                        break;
                }
            }
            
            s.Shutdown();
        }
        */

        [Test]
        public void SystemEventTests()
        {
            var ecs = new ECS(new DummyECSDatabase(), new TestLogService(), 1234, new ISystem[]
            {
                new SystemEventTestSystem(),
            });
            
            ecs.RegisterComponent<SomeValueComponent>(1);
            ecs.Initialize();

            var entityA = ecs.CreateEntity();
            ecs.AssignComponent<SomeValueComponent>(entityA);

            const int numTicks = 10;
            for (var i = 0; i < numTicks; i++)
                ecs.Tick();
            
            Assert.IsTrue(ecs.Get<SomeValueComponent>(entityA).EventIncrementingValue == numTicks);
        }

        [Test]
        public void ExternalEventTests()
        {
            var s = new ECS(new DummyECSDatabase(), new TestLogService(), 1234, new ISystem[]
            {
                new ExternalEventTestSystem(),
            });
            
            s.Initialize();

            var testValue = 0;
            void onTestExternalEvent(TestExternalEvent data)
            {
                testValue++;
            }
            
            s.Subscribe<TestExternalEvent>(onTestExternalEvent);

            const int numTicks = 10;
            
            for (var i = 0; i < numTicks; i++)
                s.Tick();
            
            s.Unsubscribe<TestExternalEvent>(onTestExternalEvent);
            
            for (var i = 0; i < numTicks; i++)
                s.Tick();
            
            Assert.IsTrue(testValue == numTicks);
        }

        [Test]
        public void UnmanagedQuickSortTests()
        {
            var arr = new []
            {
                10,
                139,
                -49,
                193545,
                1,
                -9393,
                123,
                124,
                9
            };

            var unmanagedArray = new UnmanagedArray<int>(arr);
            UnmanagedQuickSort.Sort(unmanagedArray, (a, b) => a.CompareTo(b));

            for(var i = 1; i < unmanagedArray.Length; i++)
                Assert.IsTrue(unmanagedArray[i] > unmanagedArray[i - 1]);
            
            unmanagedArray.Dispose();
        }
        
        public class DummyECSDatabase : IECSDatabase
        {
            private readonly Dictionary<int, object> _data = new Dictionary<int, object>();

            public TData Get<TData>(int id) where TData : unmanaged => (TData)_data[id];
        }

        private struct SomeValueComponent
        {
            public int IncrementingValue;
            public int RandomValue;
            public int EventIncrementingValue;
        }
        
        private class SomeValueSystem : ISystem
        {
            public void Initialize(ECS ecs) { }

            public void Process(ECS ecs)
            {
                var someValueIterator = ecs.GetIterator<SomeValueComponent>();
                while (someValueIterator.NextUnsafe(out _, out var someValueComponent))
                {
                    someValueComponent->IncrementingValue++;
                    someValueComponent->RandomValue = ecs.RNG.NextInt32();
                }
            }
        }

        private class SystemEventTestSystem : ISystem, ISystemEventListener<IncrementValueSystemEvent>
        {
            public void Initialize(ECS ecs) { }

            public void Process(ECS ecs)
            {
                var someValueComponentIterator = ecs.GetIterator<SomeValueComponent>();
                while(someValueComponentIterator.Next(out var entity, out _))
                    ecs.RaiseSystemEvent(new IncrementValueSystemEvent
                    {
                        EntityId = entity
                    });
            }
            
            public void OnSystemEvent(ECS ecs, IncrementValueSystemEvent data)
            {
                var someValueComponent = ecs.GetUnsafe<SomeValueComponent>(data.EntityId);
                someValueComponent->EventIncrementingValue++;
            }
        }

        private class ExternalEventTestSystem : ISystem
        {
            public void Initialize(ECS ecs) { }

            public void Process(ECS ecs)
            {
                ecs.QueueExternalEvent(new TestExternalEvent());
            }
        }

        public class TestLogService : ILogService
        {
            public void Info(string s)
            {
                Console.WriteLine($"[INFO] {s}");
            }

            public void Warn(string s)
            {
                Console.WriteLine($"[WARN] {s}");
            }

            public void Error(string s)
            {
                Console.WriteLine($"[ERROR] {s}");
            }

            public void Exception(Exception e)
            {
                Console.WriteLine($"[EXCEPTION] {e.Message}");
            }
        }

        private struct IncrementValueSystemEvent
        {
            public int EntityId;
        }
        
        private struct TestExternalEvent { }
    }
}