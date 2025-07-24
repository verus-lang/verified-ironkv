#![verus::trusted]
//! Translates Distributed/Protocol/SHT/Network.i.dfy

use vstd::pervasive::*;
use vstd::prelude::*;

use crate::abstract_end_point_t::*;
use crate::message_t::*;
use crate::single_message_t::*;

verus! {
pub type PMsg = SingleMessage<Message>;

/// A Packet is an abstract version of a `CPacket`.
///
/// It's isomorphic to an `LSHTPacket = LPacket<AbstractEndPoint,
/// SingleMessage<Message>>`.
pub struct Packet {
    pub dst: AbstractEndPoint,
    pub src: AbstractEndPoint,
    pub msg: PMsg,
}
}
