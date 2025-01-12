// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Collections;
using Riptide.DataStreaming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests
{
    internal class FragmentAssemblerTests
    {
        [Test]
        public void IsFullyReceived_ReturnsFalseRightAfterConstructor()
        {
            var assembler = new FragmentAssembler(5, 2);
            Assert.IsFalse(assembler.IsFullyReceived());
        }

        [Test]
        public void IsFullyReceived_ReturnsFalseWhen1OutOf2FragmentsIsAdded()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            Assert.IsFalse(assembler.IsFullyReceived());
        }

        [Test]
        public void IsFullyReceived_ReturnsTrueWhen2OutOf2FragmentsIsAdded()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            assembler.AddFragment(1, new ArraySlice<byte>(buffer, 5, 5));
            Assert.IsTrue(assembler.IsFullyReceived());
        }

        [Test]
        public void GetAssembledThrows_IfNotAssembled()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            Assert.Throws<InvalidOperationException>(() => { assembler.GetAssembledBuffer(); });
        }

        [Test]
        public void GetAssembled_BufferEqualsOriginal()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            assembler.AddFragment(1, new ArraySlice<byte>(buffer, 5, 5));

            byte[] bytes = assembler.GetAssembledBuffer();
            Assert.IsTrue(bytes != null);
            Assert.IsTrue(bytes.SequenceEqual(buffer));
        }

        [Test]
        public void IsFragmentReceived_ThrowsOnInvalidIndex()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => { assembler.IsFragmentReceived(-1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { assembler.IsFragmentReceived(2); });
        }

        [Test]
        public void IsFragmentReceived_ReturnsFalseForNonAddedFragment_AndTrueForAdded()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));

            Assert.AreEqual(true, assembler.IsFragmentReceived(0));
            Assert.AreEqual(false, assembler.IsFragmentReceived(1));
        }

        [Test]
        public void AddFragmentThrows_IfAddedAlready()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            Assert.Throws<InvalidOperationException>(() =>
            {
                assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            });
        }

        [Test]
        public void AddFragmentThrows_IfInvalidIndex()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                assembler.AddFragment(-1, new ArraySlice<byte>(buffer, 0, 5));
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                assembler.AddFragment(2, new ArraySlice<byte>(buffer, 0, 5));
            });
        }

        [Test]
        public void AddFragmentThrows_IfTooLongOrShortArraySlice()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var assembler = new FragmentAssembler(5, 2);
            Assert.Throws<InvalidOperationException>(() =>
            {
                assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 6));
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                assembler.AddFragment(1, new ArraySlice<byte>(buffer, 0, 6));
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                assembler.AddFragment(1, new ArraySlice<byte>(buffer, 0, 4));
            });
        }

        [Test]
        public void AddingFragments_OnlyChangesFragmentReceivedValueForAddedFragments()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            var assembler = new FragmentAssembler(5, 4);
            Assert.AreEqual(false, assembler.IsFragmentReceived(0));
            Assert.AreEqual(false, assembler.IsFragmentReceived(1));
            Assert.AreEqual(false, assembler.IsFragmentReceived(2));
            Assert.AreEqual(false, assembler.IsFragmentReceived(3));
            assembler.AddFragment(0, new ArraySlice<byte>(buffer, 0, 5));
            Assert.AreEqual(true, assembler.IsFragmentReceived(0));
            Assert.AreEqual(false, assembler.IsFragmentReceived(1));
            Assert.AreEqual(false, assembler.IsFragmentReceived(2));
            Assert.AreEqual(false, assembler.IsFragmentReceived(3));
            assembler.AddFragment(1, new ArraySlice<byte>(buffer, 5, 5));
            Assert.AreEqual(true, assembler.IsFragmentReceived(0));
            Assert.AreEqual(true, assembler.IsFragmentReceived(1));
            Assert.AreEqual(false, assembler.IsFragmentReceived(2));
            Assert.AreEqual(false, assembler.IsFragmentReceived(3));
            assembler.AddFragment(2, new ArraySlice<byte>(buffer, 10, 5));
            Assert.AreEqual(true, assembler.IsFragmentReceived(0));
            Assert.AreEqual(true, assembler.IsFragmentReceived(1));
            Assert.AreEqual(true, assembler.IsFragmentReceived(2));
            Assert.AreEqual(false, assembler.IsFragmentReceived(3));
            assembler.AddFragment(3, new ArraySlice<byte>(buffer, 15, 5));
            Assert.AreEqual(true, assembler.IsFragmentReceived(0));
            Assert.AreEqual(true, assembler.IsFragmentReceived(1));
            Assert.AreEqual(true, assembler.IsFragmentReceived(2));
            Assert.AreEqual(true, assembler.IsFragmentReceived(3));
        }
    }
}
