using System;
using System.Collections.Generic;
using NUnit.Framework;
using Clonus;
namespace ClonusTests
{
    [TestFixture]
    public class ClonusTests
    {
        [Test]
        public void Clone_WithSimpleObject_ShouldReturnValidClone()
        {
            var original = new TestSimpleObject();
            original.Id = 22;
            original.Name = "John Doe";
            var clone = Cloner.Clone(original);
            Assert.AreEqual(original.GetType(), clone.GetType());
            Assert.IsFalse(ReferenceEquals(clone, original), "The object returned from Cloner.Clone should not be the original object");
            Assert.AreEqual(original.Id, clone.Id);
            Assert.AreEqual(original.Name, clone.Name);
        }

        [Test]
        public void Clone_WithNestedObject_ShouldCloneObjectGraph()
        {
            var original = new TestParentObject
            {
                //Name = "Parent",
                SimpleObject = new TestSimpleObject { Id = 22, Name = "John Doe" }
            };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.GetType(), clone.GetType());
            Assert.IsFalse(ReferenceEquals(clone, original), "The object returned from Cloner.Clone should not be the original object");
            //Assert.AreEqual(original.Name, clone.Name);
            Assert.IsFalse(ReferenceEquals(clone.SimpleObject, original.SimpleObject), "The child object returned from Cloner.Clone should not be the original child object");
            Assert.AreEqual(original.SimpleObject.Id, clone.SimpleObject.Id);
            Assert.AreEqual(original.SimpleObject.Name, clone.SimpleObject.Name);
        }

        [Test]
        public void Clone_WithNestedObject_WithShallowClone_ShouldOnlyCloneTheTopLevelObject()
        {
            var original = new TestParentObject
            {
                //Name = "Parent",
                SimpleObject = new TestSimpleObject { Id = 22, Name = "John Doe" }
            };
            var clone = Cloner.Clone(original, CloneMethod.Shallow);
            Assert.AreEqual(original.GetType(), clone.GetType());
            Assert.IsFalse(ReferenceEquals(clone, original), "The object returned from Cloner.Clone should not be the original object");
            //Assert.AreEqual(original.Name, clone.Name);
            Assert.IsTrue(ReferenceEquals(clone.SimpleObject, original.SimpleObject), "The child object returned from Cloner.Clone should be the original child object");
            Assert.AreEqual(original.SimpleObject.Id, clone.SimpleObject.Id);
            Assert.AreEqual(original.SimpleObject.Name, clone.SimpleObject.Name);
        }

        [Test]
        public void Clone_WithCloneableObject_ShouldCloneObjectGraph()
        {
            var original = new TestCloneable
            {
                Id = 1
            };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(42, clone.Id);
        }

        [Test]
        public void Clone_WithNestedCloneableObject_ShouldCloneObjectGraph()
        {
            var original = new TestParentObject_WithCloneableChild
            {
                Id = 1,
                Cloneable = new TestCloneable()
            };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(1, clone.Id);
            Assert.AreEqual(42, clone.Cloneable.Id);
            Assert.IsFalse(ReferenceEquals(clone.Cloneable, original.Cloneable), "The child object returned from Cloner.Clone should not be the original child object");
        }

        [Test]
        public void Clone_WithNestedListObject_ShouldCloneObjectGraph()
        {
            var original = new TestParentObject_WithListChild
            {
                Id = 1,
                List = new List<TestSimpleObject> { new TestSimpleObject { Id = 20 }, new TestSimpleObject { Id = 21 } }
            };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(1, clone.Id);
            Assert.AreEqual(2, clone.List.Count);
            Assert.AreEqual(20, clone.List[0].Id);
            Assert.IsFalse(ReferenceEquals(clone.List[0], original.List[0]), "The child object in the List returned from Cloner.Clone should not be the original child object");
            Assert.AreEqual(21, clone.List[1].Id);
            Assert.IsFalse(ReferenceEquals(clone.List[1], original.List[0]), "The child object in the List returned from Cloner.Clone should not be the original child object");
        }

        [Test]
        public void Clone_WithInheritance_ShouldCloneFullObject()
        {
            var original = new TestInheritingObject { Id = 42, Name = "Test", Description = "My Description" };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Id, clone.Id);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.AreEqual(original.Description, clone.Description);
        }

        [Test]
        public void Clone_WithArray_ShouldWork()
        {
            var original = new string[] { "hello", "there" };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Length, clone.Length);
            for (var i = 0; i < original.Length; i++)
                Assert.AreEqual(original[i], clone[i]);
        }

        [Test]
        public void Clone_WithArrayOfValueTypes_ShouldWork()
        {
            var original = new int[] { 1, 2 };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Length, clone.Length);
            for (var i = 0; i < original.Length; i++)
                Assert.AreEqual(original[i], clone[i]);
        }

        [Test]
        public void Clone_WithObject_WithField_ArrayOfValueTypes_ShouldWork()
        {
            var original = new TestObject_ArrayValueTypeField { Field = new[] { 1, 2 } };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.IsFalse(ReferenceEquals(original.Field, clone.Field));
            Assert.AreEqual(original.Field.Length, clone.Field.Length);
            for (var i = 0; i < original.Field.Length; i++)
                Assert.AreEqual(original.Field[i], clone.Field[i]);
        }

        [Test]
        public void Clone_WithObject_WithField_ArrayOfStructTypes_ShouldWork()
        {
            var original = new TestObject_ArrayStructField { Field = new[] { new MyStruct { Name = "1" }, new MyStruct { Name = "2" } } };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.IsFalse(ReferenceEquals(original.Field, clone.Field));
            Assert.AreEqual(original.Field.Length, clone.Field.Length);
            for (var i = 0; i < original.Field.Length; i++)
                Assert.AreEqual(original.Field[i].Name, clone.Field[i].Name);
        }

        [Test]
        public void Clone_WithEmptyArray_ShouldWork()
        {
            var original = new string[] {  };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Length, clone.Length);
        }

        [Test]
        public void Clone_WithObject_WithEmptyArrayProperty_ShouldWork()
        {
            var original = new TestObject_WithArrayField();
            original.Names = new string[] { };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Names.Length, clone.Names.Length);
        }

        [Test]
        public void Clone_WithObject_WithNullArrayProperty_ShouldWork()
        {
            var original = new TestObject_WithArrayField();
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.IsNull(clone.Names);
        }

        [Test]
        public void Clone_WithCircularReference_ShouldWork()
        {
            var original = new TestObject_WithCircularReference { Id = 1, Name = "Test" };
            original.Child = original;
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.AreEqual(original.Child.Name, clone.Child.Name);
            Assert.AreEqual(original.Child.Child.Name, clone.Child.Child.Name);
            Assert.IsTrue(ReferenceEquals(clone, clone.Child), "When cloning circular references, referential integrity should be preserved");
        }

        [Test]
        public void Clone_WithNestedCircularReference_ShouldWork()
        {
            var original = new TestObject_ParentCircularReference { Id = 1, Name = "Parent" };
            original.Child = new TestObject_ChildCircularReference { Id = 2, Name = "Child" };
            original.Child.Parent = original;
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.AreEqual(original.Child.Name, clone.Child.Name);
            Assert.AreEqual(original.Child.Parent.Name, clone.Child.Parent.Name);
            Assert.IsTrue(ReferenceEquals(clone, clone.Child.Parent), "When cloning circular references, referential integrity should be preserved");
            Assert.IsTrue(ReferenceEquals(clone.Child, clone.Child.Parent.Child), "When cloning circular references, referential integrity should be preserved");
        }
        
        [Test]
        public void Clone_WithArray_WithCircularReference_ShouldWork()
        {
            var original = new TestObject_WithCircularReference[] {
                new TestObject_WithCircularReference { Id = 1, Name = "Test1" },
                new TestObject_WithCircularReference { Id = 2, Name = "Test2" }
            };
            original[0].Child = original[1];
            original[1].Child = original[0];
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            for (var i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(original[i].Name, clone[i].Name);
                Assert.AreEqual(original[i].Child.Name, clone[i].Child.Name);
                Assert.AreEqual(original[i].Child.Child.Name, clone[i].Child.Child.Name);
            }
            Assert.IsTrue(ReferenceEquals(clone[0], clone[1].Child), "When cloning circular references, referential integrity should be preserved");
            Assert.IsTrue(ReferenceEquals(clone[1], clone[0].Child), "When cloning circular references, referential integrity should be preserved");
        } 

        [Test]
        public void Clone_WithArray_WithNestedCircularReference_ShouldWork()
        {
            var original = new TestObject_ParentCircularReference[] {
                new TestObject_ParentCircularReference { Id = 1, Name = "Test1" },
                new TestObject_ParentCircularReference { Id = 2, Name = "Test2" }
            };
            original[0].Child = new TestObject_ChildCircularReference { Name = "Child", Parent = original[0] };
            original[1].Child = new TestObject_ChildCircularReference { Name = "Child", Parent = original[1] };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            for (var i = 0; i < original.Length; i++)
            {
                Assert.AreEqual(original[i].Child.Name, clone[i].Child.Name);
                Assert.AreEqual(original[i].Child.Parent.Name, clone[i].Child.Parent.Name);
                Assert.IsTrue(ReferenceEquals(clone[i], clone[i].Child.Parent), "When cloning circular references, referential integrity should be preserved");
            }
        }
        
        [Test]
        public void Clone_WithObjectField_WithValueType_ShouldWork()
        {
            var original = new TestObject_ObjectField { Field= 1 };
            var clone = Cloner.Clone(original, CloneMethod.Deep);
            Assert.AreEqual(1, clone.Field);
        }
    }

    #region classes to run tests on
    public class TestObject_ArrayValueTypeField
    {
        public int[] Field;
    }

    public class TestObject_ArrayStructField
    {
        public MyStruct[] Field;
    }

    public struct MyStruct
    {
        public string Name;
    }

    public class TestObject_ObjectField
    {
        public object Field;
    }

    public class TestObject_ParentCircularReference
    {
        public int Id;
        public string Name;
        public TestObject_ChildCircularReference Child;
    }
    public class TestObject_ChildCircularReference
    {
        public int Id;
        public string Name;
        public TestObject_ParentCircularReference Parent;
    }

    public class TestObject_WithCircularReference
    {
        public int Id;
        public string Name;
        public TestObject_WithCircularReference Child;
    }
    public class TestObject_WithArrayField
    {
        public string[] Names;
    }
    public class TestSimpleObject
    {
        Func<TestSimpleObject, TestSimpleObject> cloner = tso =>
        {
            var c = new TestSimpleObject();
            c.Name = tso.Name;
            c.Id = tso.Id;
            return c;
        };
        public string Name { get; set; }
        public int Id { get; set; }
    }

    public class TestParentObject
    {
        //public string Name { get; set; }
        public TestSimpleObject SimpleObject { get; set; }
    }

    public class TestCloneable : ICloneable
    {
        public int Id { get; set; }

        public object Clone()
        {
            var t = new TestCloneable { Id = 42 };
            return t;
        }
    }

    public class TestParentObject_WithCloneableChild
    {
        public int Id { get; set; }
        public TestCloneable Cloneable { get; set; }
    }

    public class TestParentObject_WithListChild
    {
        public int Id { get; set; }
        public IList<TestSimpleObject> List { get; set; }
    }

    public class TestInheritingObject : TestSimpleObject
    {
        public string Description { get; set; }
    }
    #endregion
}